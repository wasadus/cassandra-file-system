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
    }
}