using System.Collections.Generic;
using System.IO;

using CassandraFS;
using CassandraFS.CassandraHandler;
using CassandraFS.Models;

using FluentAssertions;

using NUnit.Framework;

namespace StorageTests
{
    public class NoDropTablesTests : DefaultSettingsTestsBase
    {
        public override Config Config =>
            new Config
                {
                    CassandraEndPoints = new List<NodeSettings>
                        {
                            new NodeSettings {Host = "127.0.0.1"}
                        },
                    MessageSpaceName = "FTPMessageSpace",
                    DropFilesTable = false,
                    DropDirectoriesTable = false,
                    DropFilesContentMetaTable = false,
                    DropFilesContentTable = false,
                    DefaultDataBufferSize = 128,
                    DefaultTTL = 60,
                    ConnectionAttemptsCount = 5,
                    ReconnectTimeout = 5000
                };

        [Test]
        public void TestCreateFileAndItExistsAfterReconnect()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            var cqlFile = GetCQLFileFromFileModel(file);
            fileRepository.WriteFile(file);

            OneTimeSetup();
            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);
            fileRepository.IsFileExists(file.Path + file.Name).Should().BeTrue();
            CompareFileModel(file, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }

        [Test]
        public void TestCreateBigFileAndItExistsAfterReconnect()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = GetTestBigFileData();
            var cqlFile = GetCQLFileFromFileModel(file);
            cqlFile.Data = new byte[0];
            fileRepository.WriteFile(file);

            OneTimeSetup();
            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);
            fileRepository.IsFileExists(file.Path + file.Name).Should().BeTrue();
            CompareFileModel(file, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }

        [Test]
        public void TestCreateDirectoryAndItExistsAfterReconnect()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            var cqlDirectory = GetCQLDirectoryFromDirectoryModel(directory);
            directoryRepository.WriteDirectory(directory);

            OneTimeSetup();
            var actualDirectory = directoryRepository.ReadDirectory(directory.Path + directory.Name);
            var actualCQLDirectory = ReadCQLDirectory(directory.Path, directory.Name);
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().BeTrue();
            CompareDirectoryModel(directory, actualDirectory);
            CompareCQLDirectory(cqlDirectory, actualCQLDirectory);
        }

        [Test]
        public void TestCreateManyFileSystemEntriesAndItExistsAfterReconnect()
        {
            var root = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(root);
            var rootPath = root.Path + root.Name;
            var directories = new DirectoryModel[50];
            var cqlDirectories = new CQLDirectory[50];
            var files = new FileModel[50];
            var cqlFiles = new CQLFile[50];

            for (var i = 0; i < 50; i++)
            {
                var directory = GetTestDirectoryModel(rootPath);
                var cqlDirectory = GetCQLDirectoryFromDirectoryModel(directory);
                directoryRepository.WriteDirectory(directory);
                directories[i] = directory;
                cqlDirectories[i] = cqlDirectory;

                var file = GetTestFileModel(rootPath);
                var cqlFile = GetCQLFileFromFileModel(file);
                fileRepository.WriteFile(file);
                files[i] = file;
                cqlFiles[i] = cqlFile;
            }

            OneTimeSetup();

            for (var i = 0; i < 50; i++)
            {
                var directory = directories[i];
                var cqlDirectory = cqlDirectories[i];
                var actualDirectory = directoryRepository.ReadDirectory(directory.Path + Path.DirectorySeparatorChar + directory.Name);
                var actualCQLDirectory = ReadCQLDirectory(directory.Path, directory.Name);
                CompareDirectoryModel(directory, actualDirectory);
                CompareCQLDirectory(cqlDirectory, actualCQLDirectory);
                CompareDirectoryStat(directory.GetStat(), actualDirectory.GetStat());

                var file = files[i];
                var cqlFile = cqlFiles[i];
                var actualFile = fileRepository.ReadFile(file.Path + Path.DirectorySeparatorChar + file.Name);
                var actualCQLFile = ReadCQLFile(file.Path, file.Name);
                CompareFileModel(file, actualFile);
                CompareCQLFile(cqlFile, actualCQLFile);
                CompareFileStat(file.GetStat(), actualFile.GetStat());
            }
        }

        [Test]
        public void TestFileIsDeletedAfterReconnect()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            fileRepository.WriteFile(file);
            fileRepository.IsFileExists(file.Path + file.Name).Should().BeTrue();
            ReadCQLFile(file.Path, file.Name).Should().NotBeNull();
            fileRepository.DeleteFile(file.Path + file.Name);
            OneTimeSetup();
            fileRepository.IsFileExists(file.Path + file.Name).Should().BeFalse();
            ReadCQLFile(file.Path, file.Name).Should().BeNull();
        }

        [Test]
        public void TestBigFileIsDeletedAfterReconnect()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = GetTestBigFileData();

            var cqlFile = GetCQLFileFromFileModel(file);
            cqlFile.Data = new byte[0];

            fileRepository.WriteFile(file);
            fileRepository.IsFileExists(file.Path + file.Name).Should().BeTrue();
            ReadCQLFile(file.Path, file.Name).Should().NotBeNull();
            fileRepository.DeleteFile(file.Path + file.Name);
            OneTimeSetup();
            fileRepository.IsFileExists(file.Path + file.Name).Should().BeFalse();
            ReadCQLFile(file.Path, file.Name).Should().BeNull();
        }

        [Test]
        public void TestDirectoryIsDeletedAfterReconnect()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(directory);
            directoryRepository.IsDirectoryExists(Path.Combine(directory.Path, directory.Name)).Should().BeTrue();
            ReadCQLDirectory(directory.Path, directory.Name).Should().NotBeNull();
            directoryRepository.DeleteDirectory(Path.Combine(directory.Path, directory.Name));
            OneTimeSetup();
            directoryRepository.IsDirectoryExists(Path.Combine(directory.Path, directory.Name)).Should().BeFalse();
            ReadCQLDirectory(directory.Path, directory.Name).Should().BeNull();
        }
    }
}