using System;
using System.IO;
using System.Text;
using System.Security.AccessControl;

using FluentAssertions;

using Newtonsoft.Json;

using NUnit.Framework;

namespace FileSystemTests
{
    public class Tests
    {
        private readonly string mountPoint = "/home/cassandra-fs/";

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestWriteValidFile()
        {
            var fileName = Guid.NewGuid().ToString();
            var fileContent = Guid.NewGuid().ToString();
            File.WriteAllText(mountPoint + fileName, fileContent, Encoding.UTF8);
            var actualContent = File.ReadAllText(mountPoint + fileName, Encoding.UTF8);
            actualContent.Should().Be(fileContent);
            var unixFileInfo = new Mono.Unix.UnixFileInfo("test.txt");
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
    }
}