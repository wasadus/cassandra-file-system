using System.Collections.Generic;

using Cassandra.Data.Linq;

using System.IO;
using System.Linq;

using Cassandra;

using Mono.Fuse.NETStandard;
using Mono.Unix.Native;
using System;
using CassandraFS.Models;

namespace CassandraFS.CassandraHandler
{
    public class DirectoryRepository
    {
        private readonly IGlobalTimestampProvider timestampProvider;
        private readonly Table<CQLDirectory> directoriesTableEvent;
        private readonly DirectoryModel root = new DirectoryModel {Path = "", Name = "", FilePermissions = FilePermissions.ACCESSPERMS | FilePermissions.S_IFDIR, GID = 0, UID = 0, ModifiedTimestamp = DateTimeOffset.Now};

        public DirectoryRepository(ISession session, IGlobalTimestampProvider timestampProvider)
        {
            this.timestampProvider = timestampProvider;
            directoriesTableEvent = new Table<CQLDirectory>(session);
        }

        public IEnumerable<DirectoryEntry> ReadDirectoryContent(string path) =>
            directoriesTableEvent
                .Where(entry => entry.Path.Equals(path))
                .Execute()
                .Select(dir => new DirectoryEntry(dir.Name) { Stat = GetDirectoryModel(dir).GetStat() });

        public bool IsDirectoriesExists(string directoryPath) =>
            directoriesTableEvent
                .Where(f => f.Path == directoryPath)
                .Execute()
                .Any();

        public void WriteDirectory(DirectoryModel directory)
           => directoriesTableEvent.Insert(GetCQLDirectory(directory)).SetTimestamp(timestampProvider.UpdateTimestamp()).Execute();

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
            
            return GetDirectoryModel(dir);
        }

        public IEnumerable<DirectoryModel> GetChildDirectories(string rootPath)
        {
            return directoriesTableEvent
                   .Where(directory => directory.Path == rootPath)
                   .Select(x => GetDirectoryModel(x))
                   .Execute();
        }

        public void DeleteDirectory(string path)
        {
            var dirName = FileSystemRepository.GetFileName(path);
            var parentDirPath = FileSystemRepository.GetParentDirectory(path);
            directoriesTableEvent
                .Where(d => d.Path.Equals(parentDirPath) && d.Name.Equals(dirName))
                .Delete()
                .SetTimestamp(timestampProvider.UpdateTimestamp())
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

        private DirectoryModel GetDirectoryModel(CQLDirectory directory) =>
            directory == null
                ? null
                : new DirectoryModel
                {
                    Path = directory.Path,
                    Name = directory.Name,
                    FilePermissions = (FilePermissions)directory.FilePermissions,
                    GID = (uint)directory.GID,
                    UID = (uint)directory.UID,
                    ModifiedTimestamp = directory.ModifiedTimestamp
                };

        private CQLDirectory GetCQLDirectory(DirectoryModel directory) => new CQLDirectory
        {
            Path = directory.Path,
            Name = directory.Name,
            FilePermissions = (int)directory.FilePermissions,
            GID = directory.GID,
            UID = directory.UID,
            ModifiedTimestamp = directory.ModifiedTimestamp
        };
    }
}