using System;
using System.Collections.Generic;
using System.Linq;

using Cassandra;
using Cassandra.Data.Linq;

using CassandraFS.BlobStorage;
using CassandraFS.Models;

using Mono.Fuse.NETStandard;
using Mono.Unix.Native;

namespace CassandraFS.CassandraHandler
{
    public class FileRepository
    {
        private readonly IGlobalTimestampProvider timestampProvider;
        private readonly Table<CQLFile> filesTableEvent;
        private readonly CqlLargeBlobStorage<CQLFileContentMeta, CQLFileContent> blobStorage;
        private readonly int TTL;
        private readonly int dataBufferSize;

        public FileRepository(ISession session, Config config, IGlobalTimestampProvider timestampProvider)
        {
            this.timestampProvider = timestampProvider;
            filesTableEvent = new Table<CQLFile>(session);
            dataBufferSize = config.DefaultDataBufferSize!.Value;
            blobStorage = new CqlLargeBlobStorage<CQLFileContentMeta, CQLFileContent>(session, dataBufferSize);
            TTL = config.DefaultTTL!.Value;
        }

        public IEnumerable<DirectoryEntry> ReadDirectoryContent(string path) =>
            filesTableEvent
                .Where(entry => entry.Path == path)
                .Execute()
                .Select(file => new DirectoryEntry(file.Name) {Stat = GetShortStat(file)});

        public bool IsFilesExists(string directoryPath) =>
            filesTableEvent
                .Where(f => f.Path == directoryPath)
                .Execute()
                .Any();

        public void WriteFile(FileModel file)
        {
            var cqlFile = GetCQLFile(file);
            var timestamp = timestampProvider.UpdateTimestamp();
            if (cqlFile.ContentGuid.HasValue)
            {
                blobStorage.Write(cqlFile.ContentGuid.Value.ToString(), file.Data, timestamp, TimeSpan.FromSeconds(TTL));
            }
            else
            {
                RemoveFileContent(file.ContentGUID, timestamp);
            }
            filesTableEvent.Insert(cqlFile).SetTTL(TTL).SetTimestamp(timestamp).Execute();
        }

        public FileModel ReadFile(string path)
        {
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var fileName = FileSystemRepository.GetFileName(path);
            var file = filesTableEvent
                       .FirstOrDefault(f => f.Path == parentDirPath && f.Name == fileName)
                       .Execute();
            var fileModel = GetFileModel(file);
            if (file?.ContentGuid != null)
            {
                fileModel.Data = blobStorage.TryRead(file.ContentGuid.Value.ToString());
            }
            return fileModel;
        }

        public void RenameFile(string from, string to)
        {
            var parentDirPath = FileSystemRepository.GetParentDirectory(from);
            var fileName = FileSystemRepository.GetFileName(from);
            var file = filesTableEvent
                       .FirstOrDefault(f => f.Path == parentDirPath && f.Name == fileName)
                       .Execute();
            file.ModifiedTimestamp = DateTimeOffset.Now;
            var newParentDirPath = FileSystemRepository.GetParentDirectory(to);
            var newFileName = FileSystemRepository.GetFileName(to);
            file.Path = newParentDirPath;
            file.Name = newFileName;
            var timestamp = timestampProvider.UpdateTimestamp();
            filesTableEvent.Insert(file).SetTTL(TTL).SetTimestamp(timestamp).Execute();
        }

        public void DeleteFile(string path)
        {
            var fileName = FileSystemRepository.GetFileName(path);
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var file = filesTableEvent
                       .FirstOrDefault(d => d.Path == parentDirPath && d.Name == fileName)
                       .Execute();
            var timestamp = timestampProvider.UpdateTimestamp();
            if (file?.ContentGuid != null)
            {
                blobStorage.TryDelete(file.ContentGuid.ToString(), timestamp);
            }

            filesTableEvent
                .Where(d => d.Path == parentDirPath && d.Name == fileName)
                .Delete()
                .SetTimestamp(timestamp)
                .Execute();
        }

        public bool IsFileExists(string path)
        {
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var fileName = FileSystemRepository.GetFileName(path);
            var file = filesTableEvent
                       .Where(f => f.Path == parentDirPath && f.Name == fileName)
                       .Execute();
            var result = file.Any();
            return result;
        }

        // Если файл большой, то не будет осуществляться запрос к второй таблице и размер будет 0
        private Stat GetShortStat(CQLFile file) => new Stat()
            {
                st_nlink = 1,
                st_mode = (FilePermissions)file.FilePermissions,
                st_size = file.Data?.LongLength ?? 0,
                st_blocks = file.Data?.LongLength / 512 ?? 0,
                st_blksize = file.Data?.LongLength ?? 0, // Optimal size for buffer in I/O operations
                st_atim = DateTimeOffset.Now.ToTimespec(), // access
                st_mtim = file.ModifiedTimestamp.ToTimespec(), // modified
                st_gid = (uint)file.GID,
                st_uid = (uint)file.UID,
            };

        private FileModel GetFileModel(CQLFile file) =>
            file == null
                ? null
                : new FileModel
                    {
                        Path = file.Path,
                        Name = file.Name,
                        Data = file.Data,
                        ModifiedTimestamp = file.ModifiedTimestamp,
                        ExtendedAttributes =
                            FileExtendedAttributesHandler.DeserializeExtendedAttributes(file.ExtendedAttributes),
                        FilePermissions = (FilePermissions)file.FilePermissions,
                        GID = (uint)file.GID,
                        UID = (uint)file.UID,
                        ContentGUID = file.ContentGuid
                    };

        private CQLFile GetCQLFile(FileModel file)
        {
            var cqlFile = new CQLFile
                {
                    Path = file.Path,
                    Name = file.Name,
                    ExtendedAttributes = FileExtendedAttributesHandler.SerializeExtendedAttributes(file.ExtendedAttributes),
                    ModifiedTimestamp = file.ModifiedTimestamp,
                    FilePermissions = (int)file.FilePermissions,
                    GID = file.GID,
                    UID = file.UID
                };
            if (file.Data.Length > dataBufferSize)
            {
                cqlFile.ContentGuid = file.ContentGUID ?? Guid.NewGuid();
            }
            else
            {
                cqlFile.Data = file.Data;
            }
            return cqlFile;
        }

        private void RemoveFileContent(Guid? contentGuid, DateTimeOffset timestamp)
        {
            if (!contentGuid.HasValue)
                return;
            blobStorage.TryDelete(contentGuid.ToString(), timestamp);
        }

        public IEnumerable<FileModel> GetChildFiles(string rootPath)
        {
            var files = filesTableEvent
                        .Where(file => file.Path == rootPath)
                        .Select(x => GetFileModel(x))
                        .Execute();
            var fileModels = files.ToList();
            foreach (var fileModel in fileModels)
            {
                if (fileModel?.ContentGUID != null)
                {
                    fileModel.Data = blobStorage.TryRead(fileModel.ContentGUID.Value.ToString());
                }
            }
            return fileModels;
        }
    }
}