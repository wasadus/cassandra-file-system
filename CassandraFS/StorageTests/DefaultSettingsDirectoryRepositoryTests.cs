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
            var now = DateTimeOffset.Now;
            WriteValidDirectory(now);
            var actualDirectory = directoryRepository.ReadDirectory(defaultDirPath + defaultDirName);
            actualDirectory.Name.Should().Be(defaultDirName);
            actualDirectory.Path.Should().Be(defaultDirPath);
            actualDirectory.FilePermissions.Should().HaveFlag(defaultDirPermissions);
            actualDirectory.GID.Should().Be(defaultDirGID);
            actualDirectory.UID.Should().Be(defaultDirUID);
            actualDirectory.ModifiedTimestamp.Should().BeCloseTo(now);
        }

        [Test]
        public void TestReplaceDirectory()
        {
            var now = DateTimeOffset.Now;
            WriteValidDirectory(now);
            var newPermissions = FilePermissions.S_IFDIR | FilePermissions.ACCESSPERMS;
            now = DateTimeOffset.Now;
            WriteValidDirectory(now, permissions : newPermissions, gid : 1, uid : 1);
            var actualDirectory = directoryRepository.ReadDirectory(defaultDirPath + defaultDirName);
            actualDirectory.Name.Should().Be(defaultDirName);
            actualDirectory.Path.Should().Be(defaultDirPath);
            actualDirectory.FilePermissions.Should().HaveFlag(newPermissions);
            actualDirectory.GID.Should().Be(1);
            actualDirectory.UID.Should().Be(1);
            actualDirectory.ModifiedTimestamp.Should().BeCloseTo(now);
        }

        [Test]
        public void TestReadNotExistingDirectory()
        {
            directoryRepository.IsDirectoryExists(defaultDirPath + defaultDirName).Should().Be(false);
            var now = DateTimeOffset.Now;
            WriteValidDirectory(now);
            directoryRepository.IsDirectoryExists(defaultDirPath + defaultDirName).Should().Be(true);
        }

        [Test]
        public void TestReadDirectoryStat()
        {
            var now = DateTimeOffset.Now;
            WriteValidDirectory(now);
            var directoryStat = directoryRepository.ReadDirectory(defaultDirPath + defaultDirName).GetStat();
            // todo (z.yarin, 22.04.2021): Сравнивать время изменения
            directoryStat.st_mtim.tv_sec.Should().BeCloseTo(now.ToTimespec().tv_sec, 1000);
            directoryStat.st_mtim.tv_nsec.Should().BeCloseTo(now.ToTimespec().tv_nsec, 1000000);
            directoryStat.st_mode.Should().HaveFlag(defaultDirPermissions);
            directoryStat.st_nlink.Should().BePositive();
            directoryStat.st_size.Should().BeGreaterOrEqualTo(0);
            directoryStat.st_gid.Should().Be(0);
            directoryStat.st_uid.Should().Be(0);
        }

        [Test]
        public void TestCreateDirectoryInsideDirectory()
        {
        }

        [Test]
        public void TestCreateManyDirectoriesInOneDirectory()
        {
        }

        [Test]
        public void TestCreateManyDirectories()
        {
        }

        [Test]
        public void TestDeleteValidDirectory()
        {
        }

        [Test]
        public void TestDeleteNotExistingDirectory()
        {
        }

        private void WriteValidDirectory(
            DateTimeOffset modifiedTimeStamp,
            string path = null,
            string name = "testdir",
            FilePermissions permissions = FilePermissions.S_IFDIR,
            uint uid = 0,
            uint gid = 0
        )
        {
            var directory = new DirectoryModel
                {
                    Name = name,
                    Path = path ?? Path.DirectorySeparatorChar.ToString(),
                    FilePermissions = permissions,
                    GID = gid,
                    UID = uid,
                    ModifiedTimestamp = modifiedTimeStamp
                };
            directoryRepository.WriteDirectory(directory);
        }
    }
}