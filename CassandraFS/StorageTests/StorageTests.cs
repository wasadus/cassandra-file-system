using System;
using System.Collections.Generic;
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
    public class Tests
    {
        private DirectoryRepository directoryRepository;
        private FileRepository fileRepository;
        private Container container;

        private void ConfigureContainer(int TTL, bool dropTables, int bufferSize)
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
                    DropFilesTable = dropTables,
                    DropDirectoriesTable = dropTables,
                    DropFilesContentMetaTable = dropTables,
                    DropFilesContentTable = dropTables,
                    DefaultDataBufferSize = bufferSize,
                    DefaultTTL = TTL,
                    ConnectionAttemptsCount = 5,
                    ReconnectTimeout = 5000
                };

            container.Configurator.ForAbstraction<Config>().UseInstances(config);
            CassandraConfigurator.ConfigureCassandra(container, logger);
            directoryRepository = container.Get<DirectoryRepository>();
            fileRepository = container.Get<FileRepository>();
        }

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestWriteValidFile()
        {
            ConfigureContainer(60, true, 1024);
            var data = Encoding.UTF8.GetBytes("SomeData");
            var now = DateTimeOffset.Now;
            var file = new FileModel
                {
                    Name = "test.txt",
                    Path = "/",
                    ExtendedAttributes = new ExtendedAttributes
                        {
                            Attributes = new Dictionary<string, byte[]> {{"attr", Encoding.UTF8.GetBytes("value")}}
                        },
                    Data = data,
                    FilePermissions = FilePermissions.S_IFREG,
                    GID = 0,
                    UID = 0,
                    ModifiedTimestamp = now
                };
            fileRepository.WriteFile(file);
            var actualFile = fileRepository.ReadFile("/test.txt");
            actualFile.Name.Should().Be("test.txt");
            actualFile.Path.Should().Be("/");
            actualFile.Data.Should().BeEquivalentTo(data);
            actualFile.ExtendedAttributes.Attributes.Keys.Should().Contain("attr");
            actualFile.ExtendedAttributes.Attributes["attr"].Should().BeEquivalentTo(Encoding.UTF8.GetBytes("value"));
            actualFile.FilePermissions.Should().HaveFlag(FilePermissions.S_IFREG);
            actualFile.GID.Should().Be(0);
            actualFile.UID.Should().Be(0);
            actualFile.ModifiedTimestamp.Should().Be(now);
        }

        [Test]
        public void TestWriteEmptyFile()
        {
            ConfigureContainer(60, true, 1024);
            var now = DateTimeOffset.Now;
            var file = new FileModel
                {
                    Name = "test.txt",
                    Path = "/",
                    ExtendedAttributes = new ExtendedAttributes
                        {
                            Attributes = new Dictionary<string, byte[]> { { "attr", Encoding.UTF8.GetBytes("value") } }
                        },
                    FilePermissions = FilePermissions.S_IFREG,
                    GID = 0,
                    UID = 0,
                    ModifiedTimestamp = now
                };
            fileRepository.WriteFile(file);
            var actualFile = fileRepository.ReadFile("/test.txt");
            actualFile.Name.Should().Be("test.txt");
            actualFile.Path.Should().Be("/");
            actualFile.Data.Should().BeNullOrEmpty();
            actualFile.ExtendedAttributes.Attributes.Keys.Should().Contain("attr");
            actualFile.ExtendedAttributes.Attributes["attr"].Should().BeEquivalentTo(Encoding.UTF8.GetBytes("value"));
            actualFile.FilePermissions.Should().HaveFlag(FilePermissions.S_IFREG);
            actualFile.GID.Should().Be(0);
            actualFile.UID.Should().Be(0);
            actualFile.ModifiedTimestamp.Should().Be(now);
        }
    }
}