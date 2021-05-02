using System;
using System.Collections.Generic;
using System.IO;
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
            var root = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
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
    }
}