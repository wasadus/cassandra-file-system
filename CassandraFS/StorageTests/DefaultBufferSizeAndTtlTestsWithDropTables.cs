using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CassandraFS;
using CassandraFS.CassandraHandler;
using CassandraFS.Models;

using FluentAssertions;

using GroboContainer.Core;
using GroboContainer.Impl;

using Mono.Unix.Native;

using NUnit.Framework;

using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.Logging.File;
using Vostok.Logging.File.Configuration;

namespace StorageTests
{
    public class DefaultBufferSizeAndTtlTestsWithDropTables
    {
        private DirectoryRepository directoryRepository;
        private FileRepository fileRepository;
        private Container container;

        private byte[] defaultFileData = Encoding.UTF8.GetBytes("SomeData");
        private string defaultFileName = "test.file";
        private string defaultFilePath = "/";
        private FilePermissions defaultFilePermissions = FilePermissions.S_IFREG;
        private uint defaultFileUID = 0;
        private uint defaultFileGID = 0;

        private string defaultDirPath = "/";
        private string defaultDirName = "testdir";
        private FilePermissions defaultDirPermissions = FilePermissions.S_IFDIR;
        private uint defaultDirUID = 0;
        private uint defaultDirGID = 0;

        private ExtendedAttributes defaultFileAttributes = new ExtendedAttributes
            {
                Attributes = new Dictionary<string, byte[]> {{"attr", Encoding.UTF8.GetBytes("value")}}
            };

        [SetUp]
        public void Setup()
        {
            var configuration = new ContainerConfiguration(typeof(CassandraFileSystem).Assembly);
            container = new Container(configuration);
            var settings = new FileLogSettings();
            var logger = new CompositeLog(new ConsoleLog(), new FileLog(settings));
            container.Configurator.ForAbstraction<ILog>().UseInstances(logger);
            var config = new Config
                {
                    CassandraEndPoints = new List<NodeSettings> {new NodeSettings {Host = "cassandra"}},
                    MessageSpaceName = "TestMessageSpace",
                    DropFilesTable = true,
                    DropDirectoriesTable = true,
                    DropFilesContentMetaTable = true,
                    DropFilesContentTable = true,
                    DefaultDataBufferSize = 2048,
                    DefaultTTL = 60,
                    ConnectionAttemptsCount = 5,
                    ReconnectTimeout = 5000
                };

            container.Configurator.ForAbstraction<Config>().UseInstances(config);
            CassandraConfigurator.ConfigureCassandra(container, logger);
            directoryRepository = container.Get<DirectoryRepository>();
            fileRepository = container.Get<FileRepository>();
        }

        [Test]
        public void TestWriteValidFile()
        {
            var now = DateTimeOffset.Now;
            WriteValidFile(defaultFileData, defaultFileAttributes, now);
            var actualFile = fileRepository.ReadFile(defaultFilePath + defaultFileName);
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
        public void TestReplaceFile()
        {
            var now = DateTimeOffset.Now;
            WriteValidDirectory(now);
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
            actualFile.ExtendedAttributes.Attributes["attr"].Should().BeEquivalentTo(newData);
            actualFile.FilePermissions.Should().HaveFlag(newPermissions);
            actualFile.GID.Should().Be(1);
            actualFile.UID.Should().Be(1);
            actualFile.ModifiedTimestamp.Should().BeCloseTo(now);
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

        private void WriteValidFile(
            byte[] fileData,
            ExtendedAttributes attributes,
            DateTimeOffset modifiedTimeStamp,
            string path = "/",
            string name = "test.file",
            FilePermissions permissions = FilePermissions.S_IFREG,
            uint gid = 0,
            uint uid = 0
        )
        {
            var file = new FileModel
                {
                    Name = name,
                    Path = path,
                    ExtendedAttributes = attributes,
                    Data = fileData,
                    FilePermissions = permissions,
                    GID = gid,
                    UID = uid,
                    ModifiedTimestamp = modifiedTimeStamp
                };
            fileRepository.WriteFile(file);
        }

        private void WriteValidDirectory(
            DateTimeOffset modifiedTimeStamp,
            string path = "/",
            string name = "testdir",
            FilePermissions permissions = FilePermissions.S_IFDIR,
            uint uid = 0,
            uint gid = 0
        )
        {
            var directory = new DirectoryModel
                {
                    Name = name,
                    Path = path,
                    FilePermissions = permissions,
                    GID = gid,
                    UID = uid,
                    ModifiedTimestamp = modifiedTimeStamp
                };
            directoryRepository.WriteDirectory(directory);
        }
    }
}