using System;
using System.IO;

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
            var cqlDirectory = GetCQLDirectoryFromDirectoryModel(directory);
            directoryRepository.WriteDirectory(directory);
            var actualDirectory = directoryRepository.ReadDirectory(directory.Path + directory.Name);
            var actualCQLDirectory = ReadCQLDirectory(directory.Path, directory.Name);
            CompareDirectoryModel(directory, actualDirectory);
            CompareCQLDirectory(cqlDirectory, actualCQLDirectory);
        }

        [Test]
        public void TestReplaceDirectory()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(directory);
            directory.FilePermissions = FilePermissions.S_IFDIR | FilePermissions.ACCESSPERMS;
            directory.GID = 1;
            directory.UID = 2;
            directoryRepository.WriteDirectory(directory);
            var actualDirectory = directoryRepository.ReadDirectory(directory.Path + directory.Name);
            var actualCQLDirectory = ReadCQLDirectory(directory.Path, directory.Name);
            var cqlDirectory = GetCQLDirectoryFromDirectoryModel(directory);
            CompareDirectoryModel(directory, actualDirectory);
            CompareCQLDirectory(cqlDirectory, actualCQLDirectory);
        }

        [Test]
        public void TestReadNotExistingDirectory()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().BeFalse();
            ReadCQLDirectory(directory.Path, directory.Name).Should().BeNull();
            directoryRepository.WriteDirectory(directory);
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().BeTrue();
            ReadCQLDirectory(directory.Path, directory.Name).Should().NotBeNull();
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
            var child2 = GetTestDirectoryModel(child1.Path + child1.Name);
            var cqlRoot = GetCQLDirectoryFromDirectoryModel(root);
            var cqlChild1 = GetCQLDirectoryFromDirectoryModel(child1);
            var cqlChild2 = GetCQLDirectoryFromDirectoryModel(child2);
            directoryRepository.WriteDirectory(root);
            directoryRepository.WriteDirectory(child1);
            directoryRepository.WriteDirectory(child2);
            var actualRoot = directoryRepository.ReadDirectory(root.Path + root.Name);
            var actualChild1 = directoryRepository.ReadDirectory(child1.Path + child1.Name);
            var actualChild2 = directoryRepository.ReadDirectory(child2.Path + child2.Name);
            var actualCQLRoot = ReadCQLDirectory(root.Path, root.Name);
            var actualCQLChild1 = ReadCQLDirectory(child1.Path, child1.Name);
            var actualCQLChild2 = ReadCQLDirectory(child2.Path, child2.Name);
            CompareDirectoryModel(root, actualRoot);
            CompareDirectoryModel(child1, actualChild1);
            CompareDirectoryModel(child2, actualChild2);
            CompareCQLDirectory(cqlRoot, actualCQLRoot);
            CompareCQLDirectory(cqlChild1, actualCQLChild1);
            CompareCQLDirectory(cqlChild2, actualCQLChild2);
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
                var cqlDirectory = GetCQLDirectoryFromDirectoryModel(directory);
                directoryRepository.WriteDirectory(directory);
                var actualDirectory = directoryRepository.ReadDirectory(directory.Path + Path.DirectorySeparatorChar + directory.Name);
                var actualCQLDirectory = ReadCQLDirectory(directory.Path, directory.Name);
                CompareDirectoryModel(directory, actualDirectory);
                CompareCQLDirectory(cqlDirectory, actualCQLDirectory);
                CompareDirectoryStat(directory.GetStat(), actualDirectory.GetStat());
            }
        }

        [Test]
        public void TestDeleteValidDirectory()
        {
            var directory = GetTestDirectoryModel(Path.DirectorySeparatorChar.ToString());
            directoryRepository.WriteDirectory(directory);
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().BeTrue();
            ReadCQLDirectory(directory.Path, directory.Name).Should().NotBeNull();
            directoryRepository.DeleteDirectory(directory.Path + directory.Name);
            directoryRepository.IsDirectoryExists(directory.Path + directory.Name).Should().BeFalse();
            ReadCQLDirectory(directory.Path, directory.Name).Should().BeNull();
        }

        [Test]
        public void TestDeleteNotExistingDirectory()
        {
            var path = Path.DirectorySeparatorChar.ToString();
            var name = Guid.NewGuid().ToString();
            directoryRepository.IsDirectoryExists(path + name).Should().BeFalse();
            ReadCQLDirectory(path, name).Should().BeNull();
            Assert.DoesNotThrow(() => directoryRepository.DeleteDirectory(path + name));
            directoryRepository.IsDirectoryExists(path + name).Should().BeFalse();
            ReadCQLDirectory(path, name).Should().BeNull();
        }
    }
}