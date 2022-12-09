using System;
using System.IO;
using System.Text;

using FluentAssertions;

using NUnit.Framework;

namespace FileSystemTests
{
    public class DirectoryTests
    {
        private readonly string mountPoint = "/home/cassandra-fs/";

        [Test]
        public void TestWriteValidDirectory()
        {
            var fileName = Guid.NewGuid().ToString();
            var fileContent = Guid.NewGuid().ToString();
            File.WriteAllText(mountPoint + fileName, fileContent, Encoding.UTF8);
            var actualContent = File.ReadAllText(mountPoint + fileName, Encoding.UTF8);
            actualContent.Should().Be(fileContent);
            var unixFileInfo = new Mono.Unix.UnixFileInfo("test.txt");
        }

        [Test]
        public void TestWriteDirectoryInNonExistingDirectoryReturnsError()
        {
            var outerDirectoryName = Guid.NewGuid().ToString();
            var outerDirectoryPath = Path.Combine(mountPoint, outerDirectoryName);

            var innerDirectoryName = Guid.NewGuid().ToString();
            var innerDirectoryPath = Path.Combine(outerDirectoryPath, innerDirectoryName);

            var info = Directory.CreateDirectory(innerDirectoryPath);  // Создаем папку, не создавая родительскую

            //info.Should().NotBeNull();
            //info.Exists.Should().BeFalse();
            // Console.Error.WriteLine(info.ToString());
            // Console.Error.WriteLine(info.Exists);
            // Console.Error.WriteLine(info.Parent);
            // Console.Error.WriteLine(info.FullName);
            //info.Parent.Should().BeNull();

            Directory.Exists(outerDirectoryName).Should().BeFalse(); //Или не должна существовать?
            Directory.Exists(innerDirectoryName).Should().BeFalse();
        }

        [Test]
        public void TestWriteDirectoryInsideFileReturnsError()
        {
            var fileName = Guid.NewGuid().ToString();
            var filePath = Path.Combine(mountPoint, fileName);
            File.Create(filePath);

            var directoryName = Path.Combine(filePath, Guid.NewGuid().ToString());
            var action = () => Directory.CreateDirectory(directoryName);
            action.Should().Throw<IOException>();
        }

        [Test]
        public void TestWriteDirectoryWithIncorrectName()
        {

        }

        [Test]
        public void TestRenameDirectory()
        {
            var directoryName = Path.Combine(mountPoint, Guid.NewGuid().ToString());
            Directory.CreateDirectory(directoryName);

            var directoryChild = Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.Combine(directoryName, directoryChild));

            var fileName = Guid.NewGuid().ToString();
            File.Create(Path.Combine(directoryName, fileName));

            var newDirectoryName = Path.Combine(mountPoint, Guid.NewGuid().ToString());
            Directory.Move(directoryName, newDirectoryName);

            Directory.Exists(directoryName).Should().BeFalse();
            Directory.Exists(newDirectoryName).Should().BeTrue();

            Directory.Exists(Path.Combine(directoryName, directoryChild)).Should().BeFalse();
            Directory.Exists(Path.Combine(newDirectoryName, directoryChild)).Should().BeTrue();

            File.Exists(Path.Combine(directoryName, fileName)).Should().BeFalse();
            File.Exists(Path.Combine(newDirectoryName, fileName)).Should().BeTrue();
        }

        [Test]
        public void TestWriteManyDirectories()
        {

        }

        [Test]
        public void TestDeleteDirectory()
        {
            var directoryName = Path.Combine(mountPoint, Guid.NewGuid().ToString());
            Directory.CreateDirectory(directoryName);

            Directory.Delete(directoryName);

            Directory.Exists(directoryName).Should().BeFalse();
        }

        [Test]
        public void TestFilesInDeletedDirectoryAreDeleted_WhenRecursive()
        {
            var directoryName = Path.Combine(mountPoint, Guid.NewGuid().ToString());
            Directory.CreateDirectory(directoryName);

            var fileName = Guid.NewGuid().ToString();
            var filePath = Path.Combine(directoryName, fileName);
            {
                using var file = File.Create(filePath);
            }

            Directory.Delete(directoryName, recursive: true);

            File.Exists(filePath).Should().BeFalse();
        }

        [Test]
        public void TestDeleteThrows_WhenDirectoryIsNotEmpty()
        {
            var directoryName = Path.Combine(mountPoint, Guid.NewGuid().ToString());
            Directory.CreateDirectory(directoryName);

            var fileName = Guid.NewGuid().ToString();
            var filePath = Path.Combine(directoryName, fileName);
            {
                using var file = File.Create(filePath);
                file.Write(Guid.NewGuid().ToByteArray());
                file.Flush();
            }

            File.Exists(filePath).Should().BeTrue();

            var action = () => Directory.Delete(directoryName, recursive: false);
            action.Should().Throw<IOException>();

            File.Exists(filePath).Should().BeTrue();
        }
    }
}