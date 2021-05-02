using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Cassandra.Data.Linq;

using CassandraFS;
using CassandraFS.CassandraHandler;
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
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            var cqlFile = GetCQLFileFromFileModel(file);
            fileRepository.WriteFile(file);
            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);
            CompareFileModel(file, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }

        [Test]
        public void TestWriteEmptyFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = new byte[0];
            var cqlFile = GetCQLFileFromFileModel(file);
            fileRepository.WriteFile(file);
            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);
            CompareFileModel(file, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }

        [Test]
        public void TestWriteNullDataFileReturnException()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = null;
            Assert.Throws<NullReferenceException>(() => fileRepository.WriteFile(file));
            ReadCQLFile(file.Path, file.Name).Should().BeNull();
        }

        [Test]
        public void TestReplaceFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            fileRepository.WriteFile(file);

            file.Data = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            file.ExtendedAttributes = new ExtendedAttributes
                {
                    Attributes = new Dictionary<string, byte[]>
                        {
                            {
                                Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())
                            }
                        }
                };
            file.FilePermissions = FilePermissions.S_IFREG | FilePermissions.ACCESSPERMS;
            file.GID = 1;
            file.UID = 2;

            fileRepository.WriteFile(file);
            var cqlFile = GetCQLFileFromFileModel(file);
            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);
            CompareFileModel(file, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }

        [Test]
        public void TestReadNotExistingFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            fileRepository.IsFileExists(file.Path + file.Name).Should().Be(false);
            ReadCQLFile(file.Path, file.Name).Should().BeNull();
            fileRepository.WriteFile(file);
            fileRepository.IsFileExists(file.Path + file.Name).Should().Be(true);
            ReadCQLFile(file.Path, file.Name).Should().NotBeNull();
        }

        [Test]
        public void TestReadFileStat()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            fileRepository.WriteFile(file);
            var actualStat = fileRepository.ReadFile(file.Path + file.Name).GetStat();
            CompareFileStat(file.GetStat(), actualStat);
        }

        [Test]
        public void TestCreateManyFilesInOneDirectory()
        {
            var root = DefaultSettingsDirectoryRepositoryTests.GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(root);
            var rootPath = root.Path + root.Name;
            for (var i = 0; i < 50; i++)
            {
                var file = GetTestFileModel(rootPath);
                var cqlFile = GetCQLFileFromFileModel(file);
                fileRepository.WriteFile(file);
                var actualFile = fileRepository.ReadFile(file.Path + Path.DirectorySeparatorChar + file.Name);
                var actualCQLDirectory = ReadCQLFile(file.Path, file.Name);
                CompareFileModel(file, actualFile);
                CompareCQLFile(cqlFile, actualCQLDirectory);
                CompareFileStat(file.GetStat(), actualFile.GetStat());
            }
        }

        [Test]
        public void TestDeleteValidFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            fileRepository.WriteFile(file);
            fileRepository.IsFileExists(file.Path + file.Name).Should().Be(true);
            ReadCQLFile(file.Path, file.Name).Should().NotBeNull();
            fileRepository.DeleteFile(file.Path + file.Name);
            fileRepository.IsFileExists(file.Path + file.Name).Should().Be(false);
            ReadCQLFile(file.Path, file.Name).Should().BeNull();
        }

        [Test]
        public void TestDeleteNotExistingFile()
        {
            var path = Path.DirectorySeparatorChar.ToString();
            var name = Guid.NewGuid().ToString();
            fileRepository.IsFileExists(path + name).Should().Be(false);
            ReadCQLFile(path, name).Should().BeNull();
            Assert.DoesNotThrow(() => fileRepository.DeleteFile(path + name));
            fileRepository.IsFileExists(path + name).Should().Be(false);
            ReadCQLFile(path, name).Should().BeNull();
        }

        [Test]
        public void TestDeleteEmptyFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = new byte[0];
            fileRepository.WriteFile(file);
            fileRepository.IsFileExists(file.Path + file.Name).Should().Be(true);
            ReadCQLFile(file.Path, file.Name).Should().NotBeNull();
            fileRepository.DeleteFile(file.Path + file.Name);
            fileRepository.IsFileExists(file.Path + file.Name).Should().Be(false);
            ReadCQLFile(file.Path, file.Name).Should().BeNull();
        }

        private FileModel GetTestFileModel(string path, FilePermissions permissions = FilePermissions.S_IFDIR, uint gid = 0, uint uid = 0)
        {
            path = path == Path.DirectorySeparatorChar.ToString() ? path : path + Path.DirectorySeparatorChar;
            return new FileModel()
                {
                    Name = Guid.NewGuid().ToString(),
                    Path = path,
                    FilePermissions = FilePermissions.S_IFREG | permissions,
                    GID = gid,
                    UID = uid,
                    ModifiedTimestamp = DateTimeOffset.Now,
                    Data = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()),
                    ExtendedAttributes = new ExtendedAttributes
                        {
                            Attributes = new Dictionary<string, byte[]> {{Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())}}
                        }
                };
        }

        private void CompareFileModel(FileModel expected, FileModel actual)
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
            actual.Data.Should().BeEquivalentTo(expected.Data);
            actual.ExtendedAttributes.Attributes.Keys.Should().BeEquivalentTo(expected.ExtendedAttributes.Attributes.Keys);
            actual.ExtendedAttributes.Attributes.Values.Should().BeEquivalentTo(expected.ExtendedAttributes.Attributes.Values);
        }

        private void CompareFileStat(Stat expected, Stat actual)
        {
            actual.st_mtim.tv_sec.Should().BeCloseTo(expected.st_mtim.tv_sec, 1000);
            actual.st_mtim.tv_nsec.Should().BeCloseTo(expected.st_mtim.tv_nsec, 1000000);
            actual.st_mode.Should().HaveFlag(expected.st_mode);
            actual.st_nlink.Should().BeGreaterOrEqualTo(expected.st_nlink);
            actual.st_size.Should().BeGreaterOrEqualTo(expected.st_size);
            actual.st_gid.Should().Be(expected.st_gid);
            actual.st_uid.Should().Be(expected.st_uid);
            actual.st_size.Should().BeGreaterOrEqualTo(expected.st_size);
            actual.st_blocks.Should().BeGreaterOrEqualTo(expected.st_blocks);
        }

        private void CompareCQLFile(CQLFile expected, CQLFile actual)
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
            actual.FilePermissions.Should().BeGreaterOrEqualTo(expected.FilePermissions);
            actual.Data.Should().BeEquivalentTo(expected.Data);
            actual.ExtendedAttributes.Should().BeEquivalentTo(expected.ExtendedAttributes);
        }

        private CQLFile GetCQLFileFromFileModel(FileModel file) => new CQLFile
            {
                Path = file.Path,
                Name = file.Name,
                ExtendedAttributes = FileExtendedAttributesHandler.SerializeExtendedAttributes(file.ExtendedAttributes),
                ModifiedTimestamp = file.ModifiedTimestamp,
                FilePermissions = (int)file.FilePermissions,
                GID = file.GID,
                UID = file.UID,
                Data = file.Data
            };

        private CQLFile ReadCQLFile(string path, string name)
            => filesTableEvent.FirstOrDefault(x => x.Path == path && x.Name == name).Execute();
    }
}