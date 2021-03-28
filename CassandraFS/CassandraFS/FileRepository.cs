using System;
using System.Collections.Generic;
using System.Linq;

using Cassandra;
using Cassandra.Data.Linq;

using Mono.Fuse.NETStandard;
using Mono.Unix.Native;

namespace CassandraFS
{
    public class FileRepository
    {
        private readonly Table<CQLFile> filesTableEvent;
        private readonly Table<CQLFileContent> filesContentTableEvent;
        private readonly int TTL;
        private readonly int dataBufferSize;

        public FileRepository(ISession session, Config config)
        {
            filesTableEvent = new Table<CQLFile>(session);
            filesContentTableEvent = new Table<CQLFileContent>(session);
            dataBufferSize = config.DefaultDataBufferSize!.Value;
            TTL = config.DefaultTTL!.Value;
        }

        public IEnumerable<DirectoryEntry> ReadDirectoryContent(string path) =>
            filesTableEvent
                .Where(entry => entry.Path.Equals(path))
                .Execute()
                .Select(file => new DirectoryEntry(file.Name) { Stat = GetShortStat(file) });

        public bool IsFilesExists(string directoryPath) =>
            filesTableEvent
                .Where(f => f.Path == directoryPath)
                .Execute()
                .Any();

        public void WriteFile(FileModel file)
           => filesTableEvent.Insert(GetCQLFile(file)).SetTTL(TTL).Execute();

        public FileModel ReadFile(string path)
        {
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var fileName = FileSystemRepository.GetFileName(path);
            var file = filesTableEvent
                       .FirstOrDefault(f => f.Path.Equals(parentDirPath) && f.Name.Equals(fileName))
                       .Execute();
            return file == null ? null : GetFileModel(file);
        }

        public void DeleteFile(string path)
        {
            var fileName = FileSystemRepository.GetFileName(path);
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var file = filesTableEvent
                       .FirstOrDefault(d => d.Path.Equals(parentDirPath) && d.Name.Equals(fileName)).Execute();
            if (file.ContentGuid != null)
            {
                filesContentTableEvent
                    .Where(f => f.GUID.Equals(file.ContentGuid))
                    .Delete()
                    .Execute();
            }

            filesTableEvent
                .Where(d => d.Path.Equals(parentDirPath) && d.Name.Equals(fileName))
                .Delete()
                .Execute();
        }

        public bool IsFileExists(string path)
        {
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var fileName = FileSystemRepository.GetFileName(path);
            var file = filesTableEvent
                       .Where(f => f.Path.Equals(parentDirPath) && f.Name.Equals(fileName))
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

        private FileModel GetFileModel(CQLFile file)
        {
            if (file.ContentGuid != null)
            {
                file.Data = filesContentTableEvent
                            .FirstOrDefault(f => f.GUID.Equals(file.ContentGuid))
                            .Execute().Data;
            }

            return new FileModel
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
        }

        private CQLFile GetCQLFile(FileModel file)
        {
            var cqlFile = new CQLFile
            {
                Path = file.Path,
                Name = file.Name,
                ExtendedAttributes = FileExtendedAttributesHandler.SerializeExtendedAttributes(file.ExtendedAttributes),
                ModifiedTimestamp = file.ModifiedTimestamp,
                ContentGuid = null,
                FilePermissions = (int)file.FilePermissions,
                GID = file.GID,
                UID = file.UID
            };
            if (file.Data.Length > dataBufferSize)
            {
                var guid = file.ContentGUID == null ? Guid.NewGuid() : file.ContentGUID;
                cqlFile.ContentGuid = guid;
                var cqlFileContent = new CQLFileContent { GUID = guid, Data = file.Data };
                filesContentTableEvent.Insert(cqlFileContent).SetTTL(TTL).Execute();
            }
            else
            {
                filesContentTableEvent
                    .Where(fileContent => fileContent.GUID.Equals(file.ContentGUID))
                    .Delete()
                    .Execute();
                cqlFile.Data = file.Data;
            }
            return cqlFile;
        }
    }
}