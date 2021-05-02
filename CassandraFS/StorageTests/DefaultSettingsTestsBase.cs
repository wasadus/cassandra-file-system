using System.Collections.Generic;

using Cassandra;
using Cassandra.Data.Linq;

using CassandraFS;
using CassandraFS.CassandraHandler;

using GroboContainer.Core;
using GroboContainer.Impl;

using NUnit.Framework;

using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.Logging.File;
using Vostok.Logging.File.Configuration;

namespace StorageTests
{
    public class DefaultSettingsTestsBase
    {
        public Container container;

        public DirectoryRepository directoryRepository;
        public FileRepository fileRepository;
        public Table<CQLDirectory> directoriesTableEvent;
        public Table<CQLFile> filesTableEvent;
        public int dataBufferSize;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var configuration = new ContainerConfiguration(typeof(CassandraFileSystem).Assembly);
            container = new Container(configuration);
            var settings = new FileLogSettings();
            var logger = new CompositeLog(new ConsoleLog(), new FileLog(settings));
            container.Configurator.ForAbstraction<ILog>().UseInstances(logger);
            var config = new Config
                {
                    CassandraEndPoints = new List<NodeSettings> {new NodeSettings {Host = "127.0.0.1"}},
                    MessageSpaceName = "FTPMessageSpace",
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
            var session = container.Get<ISession>();
            directoriesTableEvent = new Table<CQLDirectory>(session);
            filesTableEvent = new Table<CQLFile>(session);
            dataBufferSize = config.DefaultDataBufferSize!.Value;
        }
    }
}