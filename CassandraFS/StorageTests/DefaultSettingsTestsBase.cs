using System.Collections.Generic;
using System.IO;
using System.Text;

using Cassandra;

using CassandraFS;
using CassandraFS.CassandraHandler;
using CassandraFS.Models;

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
    public class DefaultSettingsTestsBase
    {
        public Container container;

        public DirectoryRepository directoryRepository;

        public FileRepository fileRepository;
        public byte[] defaultFileData = Encoding.UTF8.GetBytes("SomeData");
        public string defaultFileName = "test.file";
        public string defaultFilePath = Path.DirectorySeparatorChar.ToString();
        public FilePermissions defaultFilePermissions = FilePermissions.S_IFREG;
        public uint defaultFileUID = 0;
        public uint defaultFileGID = 0;

        public ExtendedAttributes defaultFileAttributes = new ExtendedAttributes
            {
                Attributes = new Dictionary<string, byte[]> {{"attr", Encoding.UTF8.GetBytes("value")}}
            };

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
        }
    }
}