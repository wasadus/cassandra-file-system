using System;
using System.IO;

using CassandraFS;
using CassandraFS.CassandraHandler;
using CassandraFS.Models;

using FluentAssertions;

using GroboContainer.Core;
using GroboContainer.Impl;

using Newtonsoft.Json;

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
        [SetUp]
        public void Setup()
        {
            var configuration = new ContainerConfiguration(typeof(CassandraFileSystem).Assembly);
            var container = new Container(configuration);

            var settings = new FileLogSettings();
            var logger = new CompositeLog(new ConsoleLog(), new FileLog(settings));
            container.Configurator.ForAbstraction<ILog>().UseInstances(logger);

            var configJson = @"
{
  ""CassandraEndPoints"": [
    {
      ""Host"": ""cassandra""
    }
  ],
  ""MessageSpaceName"": ""FTPMessageSpace"",
  ""DropFilesTable"": false,
  ""DropFilesContentTable"": false,
  ""DropFilesContentMetaTable"": false,
  ""DropDirectoriesTable"": false,
  ""ConnectionAttemptsCount"": 5,
  ""ReconnectTimeout"": 5000,
  ""DefaultTTL"": null,
  ""DefaultDataBufferSize"": 2048
}
               ";
            var config = JsonConvert.DeserializeObject<Config>(configJson);
            config.DefaultTTL ??= new TimeSpan(1, 0, 0, 0).Seconds;
            config.DefaultDataBufferSize ??= 2048;
            container.Configurator.ForAbstraction<Config>().UseInstances(config);
            CassandraConfigurator.ConfigureCassandra(container, logger);
            directoryRepository = container.Get<DirectoryRepository>();
            fileRepository = container.Get<FileRepository>();
        }

        [Test]
        public void Test1()
        {
            fileRepository.WriteFile(new FileModel(){});
            var file = fileRepository.ReadFile("");
            file.Name.Should().BeNull();
        }
    }
}