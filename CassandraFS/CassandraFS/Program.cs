using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

using Cassandra;

using GroboContainer.Core;
using GroboContainer.Impl;

using Newtonsoft.Json;

using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.Logging.File;
using Vostok.Logging.File.Configuration;

namespace CassandraFS
{
    public class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ContainerConfiguration(typeof(CassandraFileSystem).Assembly);
            var container = new Container(configuration);

            var settings = new FileLogSettings();
            var logger = new CompositeLog(new ConsoleLog(), new FileLog(settings));
            container.Configurator.ForAbstraction<ILog>().UseInstances(logger);

            var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
            config.DefaultTTL ??= new TimeSpan(14, 0, 0, 0).Seconds;
            config.DefaultDataBufferSize ??= 2048;
            container.Configurator.ForAbstraction<Config>().UseInstances(config);

            CassandraConfigurator.ConfigureCassandra(container, logger);

            logger.Info("Creating filesystem...");

            var constructor = container.GetCreationFunc<string[], CassandraFileSystem>();
            using var fs = constructor(args);
            logger.Info("Starting filesystem");
            fs.Start();
        }
    }
}