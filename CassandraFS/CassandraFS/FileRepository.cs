using System;
using System.Collections.Generic;
using System.Linq;

using Cassandra;
using Cassandra.Data.Linq;

using Mono.Fuse.NETStandard;

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
                .Select(file => new DirectoryEntry(file.Name));

        public bool IsFilesExists(string directoryPath) =>
            filesTableEvent
                .Where(f => f.Path == directoryPath)
                .Execute()
                .Any();

        public FileModel ReadFile(string path)
        {
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var fileName = FileSystemRepository.GetFileName(path);
            var file = filesTableEvent
                       .FirstOrDefault(f => f.Path.Equals(parentDirPath) && f.Name.Equals(fileName))
                       .Execute();
            if (file == null)
            {
                return null;
            }

            if (!file.ContentGuid.Equals(Guid.Empty))
            {
                file.Data = filesContentTableEvent
                            .FirstOrDefault(f => f.GUID.Equals(file.ContentGuid))
                            .Execute().Data;
            }

            return new FileModel
                {
                    Path = file.Path, Name = file.Name, Data = file.Data, ModifiedTimestamp = file.ModifiedTimestamp,
                    ExtendedAttributes =
                        FileExtendedAttributesHandler.DeserializeExtendedAttributes(file.ExtendedAttributes)
                };
        }

        public void WriteFile(FileModel file)
        {
            var cqlFile = new CQLFile
                {
                    Path = file.Path,
                    Name = file.Name,
                    ExtendedAttributes = FileExtendedAttributesHandler.SerializeExtendedAttributes(file.ExtendedAttributes),
                    ModifiedTimestamp = file.ModifiedTimestamp,
                    ContentGuid = Guid.Empty
                };
            if (file.Data.Length > dataBufferSize)
            {
                var guid = Guid.NewGuid();
                cqlFile.ContentGuid = guid;
                var cqlFileContent = new CQLFileContent {GUID = guid, Data = file.Data};
                filesContentTableEvent.Insert(cqlFileContent).SetTTL(TTL).Execute();
            }
            else
            {
                cqlFile.Data = file.Data;
            }

            filesTableEvent.Insert(cqlFile).SetTTL(TTL).Execute();
        }

        public void DeleteFile(string path)
        {
            var fileName = FileSystemRepository.GetFileName(path);
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var file = filesTableEvent
                       .FirstOrDefault(d => d.Path.Equals(parentDirPath) && d.Name.Equals(fileName)).Execute();
            if (!file.ContentGuid.Equals(Guid.Empty))
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
    }
}