using System.Collections.Generic;

using Cassandra.Data.Linq;

using System.IO;
using System.Linq;

using Cassandra;

using Mono.Fuse.NETStandard;
using Mono.Unix.Native;
using System;

namespace CassandraFS
{
    public class DirectoryRepository
    {
        private readonly Table<CQLDirectory> directoriesTableEvent;
        private readonly DirectoryModel root = new DirectoryModel {Path = "", Name = "", FilePermissions = FilePermissions.ACCESSPERMS | FilePermissions.S_IFDIR, GID = 0, UID = 0};

        public DirectoryRepository(ISession session)
        {
            directoriesTableEvent = new Table<CQLDirectory>(session);
        }

        public IEnumerable<DirectoryEntry> ReadDirectoryContent(string path) =>
            directoriesTableEvent
                .Where(entry => entry.Path.Equals(path))
                .Execute()
                .Select(dir => new DirectoryEntry(dir.Name) { Stat = GetDirectoryStat(GetDirectoryModel(dir)) });

        public bool IsDirectoriesExists(string directoryPath) =>
            directoriesTableEvent
                .Where(f => f.Path == directoryPath)
                .Execute()
                .Any();

        public Stat GetDirectoryStat(DirectoryModel directory) => new Stat()
        {
            st_atim = DateTimeOffset.Now.ToTimespec(),
            st_mtim = DateTimeOffset.Now.ToTimespec(),
            st_gid = directory.GID,
            st_uid = directory.UID,
            st_mode = directory.FilePermissions,
            st_nlink = 1,
            st_size = long.MaxValue,
            st_blocks = long.MaxValue / 512,
            st_blksize = long.MaxValue,
        };

        public void WriteDirectory(DirectoryModel directory)
           => directoriesTableEvent.Insert(GetCQLDirectory(directory)).Execute();

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
            
            return dir == null ? null : GetDirectoryModel(dir);
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
            if (path.Equals("/") || path.Equals("."))
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

        private DirectoryModel GetDirectoryModel(CQLDirectory directory) => new DirectoryModel
        {
            Path = directory.Path,
            Name = directory.Name,
            FilePermissions = directory.FilePermissions,
            GID = directory.GID,
            UID = directory.UID
        };

        private CQLDirectory GetCQLDirectory(DirectoryModel directory) => new CQLDirectory
        {
            Path = directory.Path,
            Name = directory.Name,
            FilePermissions = directory.FilePermissions,
            GID = directory.GID,
            UID = directory.UID
        };
    }
}