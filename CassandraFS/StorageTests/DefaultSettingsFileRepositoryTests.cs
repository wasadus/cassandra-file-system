using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using CassandraFS.Models;

using FluentAssertions;

using Mono.Unix.Native;

using NUnit.Framework;

namespace StorageTests
{
    public class DefaultSettingsFileRepositoryTests : DefaultSettingsTestsBase
    {
        [Test]
        public void TestWriteValidFile()
        {
            var now = DateTimeOffset.Now;
            WriteValidFile(defaultFileData, defaultFileAttributes, now);
            var actualFile = fileRepository.ReadFile(defaultFilePath + defaultFileName);
            actualFile.Should().NotBeNull();
            actualFile.Name.Should().Be(defaultFileName);
            actualFile.Path.Should().Be(defaultFilePath);
            actualFile.Data.Should().BeEquivalentTo(defaultFileData);
            actualFile.ExtendedAttributes.Attributes.Keys.Should().Contain(defaultFileAttributes.Attributes.Keys.First());
            actualFile.ExtendedAttributes.Attributes["attr"].Should().BeEquivalentTo(defaultFileAttributes.Attributes.Values.First());
            actualFile.FilePermissions.Should().HaveFlag(defaultFilePermissions);
            actualFile.GID.Should().Be(defaultFileGID);
            actualFile.UID.Should().Be(defaultFileUID);
            actualFile.ModifiedTimestamp.Should().BeCloseTo(now);
        }

        [Test]
        public void TestWriteEmptyFile()
        {
            var now = DateTimeOffset.Now;
            WriteValidFile(new byte[0], defaultFileAttributes, now);
            var actualFile = fileRepository.ReadFile(defaultFilePath + defaultFileName);
            actualFile.Name.Should().Be(defaultFileName);
            actualFile.Path.Should().Be(defaultFilePath);
            actualFile.Data.Should().BeEquivalentTo(new byte[0]);
            actualFile.ExtendedAttributes.Attributes.Keys.Should().Contain(defaultFileAttributes.Attributes.Keys.First());
            actualFile.ExtendedAttributes.Attributes["attr"].Should().BeEquivalentTo(defaultFileAttributes.Attributes.Values.First());
            actualFile.FilePermissions.Should().HaveFlag(defaultFilePermissions);
            actualFile.GID.Should().Be(defaultFileGID);
            actualFile.UID.Should().Be(defaultFileUID);
            actualFile.ModifiedTimestamp.Should().BeCloseTo(now);
        }

        [Test]
        public void TestWriteNullDataFileReturnException()
        {
            var now = DateTimeOffset.Now;
            Assert.Throws<NullReferenceException>(() => WriteValidFile(null, defaultFileAttributes, now));
        }

        [Test]
        public void TestReplaceFile()
        {
            var now = DateTimeOffset.Now;
            WriteValidFile(defaultFileData, defaultFileAttributes, now);
            now = DateTimeOffset.Now;
            var newData = Encoding.UTF8.GetBytes("New data");
            var newAttributes = new ExtendedAttributes {Attributes = new Dictionary<string, byte[]> {{"first", newData}}};
            var newPermissions = FilePermissions.S_IFREG | FilePermissions.ACCESSPERMS;
            WriteValidFile(
                newData,
                newAttributes,
                now,
                permissions : newPermissions,
                gid : 1,
                uid : 1
            );
            var actualFile = fileRepository.ReadFile(defaultFilePath + defaultFileName);
            actualFile.Name.Should().Be(defaultFileName);
            actualFile.Path.Should().Be(defaultFilePath);
            actualFile.Data.Should().BeEquivalentTo(newData);
            actualFile.ExtendedAttributes.Attributes.Keys.Should().Contain("first");
            actualFile.ExtendedAttributes.Attributes["first"].Should().BeEquivalentTo(newData);
            actualFile.FilePermissions.Should().HaveFlag(newPermissions);
            actualFile.GID.Should().Be(1);
            actualFile.UID.Should().Be(1);
            actualFile.ModifiedTimestamp.Should().BeCloseTo(now);
        }

        [Test]
        public void TestReadNotExistingFile()
        {
            fileRepository.IsFileExists(defaultFilePath + defaultFileName).Should().Be(false);
            var now = DateTimeOffset.Now;
            WriteValidFile(defaultFileData, defaultFileAttributes, now);
            fileRepository.IsFileExists(defaultFilePath + defaultFileName).Should().Be(true);
        }

        [Test]
        public void TestReadFileStat()
        {
            var now = DateTimeOffset.Now;
            WriteValidFile(defaultFileData, defaultFileAttributes, now);
            var fileStat = fileRepository.ReadFile(defaultFilePath + defaultFileName).GetStat();
            // todo (z.yarin, 22.04.2021): Сравнивать время изменения
            fileStat.st_mode.Should().HaveFlag(defaultFilePermissions);
            fileStat.st_nlink.Should().BePositive();
            fileStat.st_size.Should().Be(defaultFileData.Length);
            fileStat.st_gid.Should().Be(0);
            fileStat.st_uid.Should().Be(0);
        }

        [Test]
        public void TestCreateFileInsideDirectory()
        {
        }

        [Test]
        public void TestCreateManyFilesInOneDirectory()
        {
        }

        [Test]
        public void TestCreateManyFiles()
        {
        }

        [Test]
        public void TestDeleteValidFile()
        {
        }

        [Test]
        public void TestDeleteNotExistingFile()
        {
        }

        [Test]
        public void TestDeleteEmptyFile()
        {
        }

        private void WriteValidFile(
            byte[] fileData,
            ExtendedAttributes attributes,
            DateTimeOffset modifiedTimeStamp,
            string path = null,
            string name = "test.file",
            FilePermissions permissions = FilePermissions.S_IFREG,
            uint gid = 0,
            uint uid = 0
        )
        {
            var file = new FileModel
                {
                    Name = name,
                    Path = path ?? Path.DirectorySeparatorChar.ToString(),
                    ExtendedAttributes = attributes,
                    Data = fileData,
                    FilePermissions = permissions,
                    GID = gid,
                    UID = uid,
                    ModifiedTimestamp = modifiedTimeStamp
                };
            fileRepository.WriteFile(file);
        }
    }
}