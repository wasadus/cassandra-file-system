using System;
using System.IO;

using CassandraFS;
using CassandraFS.Models;

using FluentAssertions;

using Mono.Unix.Native;

using NUnit.Framework;

namespace StorageTests
{
    public class DefaultSettingsDirectoryRepositoryTests : DefaultSettingsTestsBase
    {
        [Test]
        public void TestWriteValidDirectory()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(directory);
            var actualDirectory = directoryRepository.ReadDirectory(directory.Path + directory.Name);
            CompareDirectoryModel(directory, actualDirectory);
        }

        [Test]
        public void TestReplaceDirectory()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(directory);
            directory.FilePermissions = FilePermissions.S_IFDIR | FilePermissions.ACCESSPERMS;
            directory.GID = 1;
            directory.UID = 1;
            directoryRepository.WriteDirectory(directory);
            var actualDirectory = directoryRepository.ReadDirectory(directory.Path + directory.Name);
            CompareDirectoryModel(directory, actualDirectory);
        }

        [Test]
        public void TestReadNotExistingDirectory()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().Be(false);
            directoryRepository.WriteDirectory(directory);
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().Be(true);
        }

        [Test]
        public void TestReadDirectoryStat()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(directory);
            var actualStat = directoryRepository.ReadDirectory(directory.Path + directory.Name).GetStat();
            CompareDirectoryStat(directory.GetStat(), actualStat);
        }

        [Test]
        public void TestWriteDirectoryInsideDirectory()
        {
            var root = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            var child1 = GetTestDirectoryModel(root.Path + root.Name);
            var child2 = GetTestDirectoryModel(child1.Path + Path.DirectorySeparatorChar + child1.Name);
            directoryRepository.WriteDirectory(root);
            directoryRepository.WriteDirectory(child1);
            directoryRepository.WriteDirectory(child2);
            var actualRoot = directoryRepository.ReadDirectory(root.Path + root.Name);
            var actualChild1 = directoryRepository.ReadDirectory(child1.Path + Path.DirectorySeparatorChar + child1.Name);
            var actualChild2 = directoryRepository.ReadDirectory(child2.Path + Path.DirectorySeparatorChar + child2.Name);
            CompareDirectoryModel(root, actualRoot);
            CompareDirectoryModel(child1, actualChild1);
            CompareDirectoryModel(child2, actualChild2);
            CompareDirectoryStat(root.GetStat(), actualRoot.GetStat());
            CompareDirectoryStat(child1.GetStat(), actualChild1.GetStat());
            CompareDirectoryStat(child2.GetStat(), actualChild2.GetStat());
        }

        [Test]
        public void TestWriteManyDirectoriesInOneDirectory()
        {
            var root = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(root);
            var rootPath = root.Path + root.Name;
            for (var i = 0; i < 50; i++)
            {
                var directory = GetTestDirectoryModel(rootPath);
                directoryRepository.WriteDirectory(directory);
                var actualDirectory = directoryRepository.ReadDirectory(directory.Path + Path.DirectorySeparatorChar + directory.Name);
                CompareDirectoryModel(directory, actualDirectory);
                CompareDirectoryStat(directory.GetStat(), actualDirectory.GetStat());
            }
        }

        [Test]
        public void TestDeleteValidDirectory()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(directory);
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().Be(true);
            directoryRepository.DeleteDirectory(directory.);
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().Be(true)
        }

        [Test]
        public void TestDeleteNotExistingDirectory()
        {
        }

        private DirectoryModel GetTestDirectoryModel(string path, FilePermissions permissions = FilePermissions.S_IFDIR, uint gid = 0, uint uid = 0)
        {
            return new DirectoryModel()
                {
                    Name = Guid.NewGuid().ToString(),
                    Path = path,
                    FilePermissions = FilePermissions.S_IFDIR | permissions,
                    GID = gid,
                    UID = uid,
                    ModifiedTimestamp = DateTimeOffset.Now
                };
        }

        private void CompareDirectoryModel(DirectoryModel expected, DirectoryModel actual)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            expected.Should().NotBeNull();
            actual.Should().NotBeNull();
            actual.Name.Should().Be(expected.Name);
            actual.Path.Should().Be(expected.Path);
            actual.GID.Should().Be(expected.GID);
            actual.UID.Should().Be(expected.UID);
            actual.ModifiedTimestamp.Should().BeCloseTo(expected.ModifiedTimestamp);
            actual.FilePermissions.Should().HaveFlag(expected.FilePermissions);
        }

        private void CompareDirectoryStat(Stat expected, Stat actual)
        {
            actual.st_mtim.tv_sec.Should().BeCloseTo(expected.st_mtim.tv_sec, 1000);
            actual.st_mtim.tv_nsec.Should().BeCloseTo(expected.st_mtim.tv_nsec, 1000000);
            actual.st_mode.Should().HaveFlag(expected.st_mode);
            actual.st_nlink.Should().BeGreaterOrEqualTo(expected.st_nlink);
            actual.st_size.Should().BeGreaterOrEqualTo(expected.st_size);
            actual.st_gid.Should().Be(expected.st_gid);
            actual.st_uid.Should().Be(expected.st_uid);
        }
    }
}