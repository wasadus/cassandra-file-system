using System.Collections.Generic;

using Cassandra.Data.Linq;

using System.IO;
using System.Linq;

using Cassandra;

using Mono.Fuse.NETStandard;

namespace CassandraFS
{
    public class DirectoryRepository
    {
        private readonly Table<CQLDirectory> directoriesTableEvent;
        private readonly DirectoryModel root = new DirectoryModel {Path = "", Name = ""};

        public DirectoryRepository(ISession session)
        {
            directoriesTableEvent = new Table<CQLDirectory>(session);
        }

        public IEnumerable<DirectoryEntry> ReadDirectoryContent(string path) =>
            directoriesTableEvent
                .Where(entry => entry.Path.Equals(path))
                .Execute()
                .Select(dir => new DirectoryEntry(dir.Name));

        public bool IsDirectoriesExists(string directoryPath) =>
            directoriesTableEvent
                .Where(f => f.Path == directoryPath)
                .Execute()
                .Any();

        public DirectoryModel ReadDirectory(string path)
        {
            if (path.Equals("/") || path.Equals("."))
            {
                return root;
            }

            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var dirName = Path.GetFileName(path);
            var dir = directoriesTableEvent
                      .FirstOrDefault(d => d.Path.Equals(parentDirPath) && d.Name.Equals(dirName))
                      .Execute();
            return dir == null ? null : new DirectoryModel {Path = dir.Path, Name = dir.Name};
        }

        public void WriteDirectory(DirectoryModel directory)
        {
            var dir = new CQLDirectory {Path = directory.Path, Name = directory.Name};
            directoriesTableEvent.Insert(dir).Execute();
        }

        public void DeleteDirectory(string path)
        {
            var dirName = FileSystemRepository.GetFileName(path);
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            directoriesTableEvent
                .Where(d => d.Path.Equals(parentDirPath) && d.Name.Equals(dirName))
                .Delete()
                .Execute();
        }

        public bool IsDirectoryExists(string path)
        {
            if (path.Equals("/"))
            {
                return true;
            }

            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            var dirName = FileSystemRepository.GetFileName(path);
            var dir = directoriesTableEvent
                      .Where(d => d.Path.Equals(parentDirPath) && d.Name.Equals(dirName))
                      .Execute();
            var result = dir.Any();
            return result;
        }
    }
}