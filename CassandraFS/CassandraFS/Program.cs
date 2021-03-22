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

            ConfigureCassandra(container, logger);

            logger.Info("Creating filesystem...");

            var constructor = container.GetCreationFunc<string[], CassandraFileSystem>();
            using var fs = constructor(args);
            logger.Info("Starting filesystem");
            fs.Start();
        }

        private static void ConfigureCassandra(IContainer container, ILog logger)
        {
            logger.Info("Configuring cassandra");
            var config = container.Get<Config>();
            var session = ConnectToCassandra(config, logger);
            container.Configurator.ForAbstraction<ISession>().UseInstances(session);

            session.CreateKeyspaceIfNotExists(config.MessageSpaceName);

            if (config.DropDirectoriesTable)
            {
                logger.Info("Dropping directories");
                session.Execute($"DROP TABLE IF EXISTS \"{config.MessageSpaceName}\".\"Directories\";");
            }

            if (config.DropFilesContentTable)
            {
                logger.Info("Dropping files content");
                session.Execute($"DROP TABLE IF EXISTS \"{config.MessageSpaceName}\".\"FilesContent\";");
            }

            if (config.DropFilesTable)
            {
                logger.Info("Dropping files");
                session.Execute($"DROP TABLE IF EXISTS \"{config.MessageSpaceName}\".\"Files\";");
            }

            logger.Info("Creating tables...");
            session.Execute(
                $"CREATE TABLE IF NOT EXISTS \"{config.MessageSpaceName}\".\"Directories\" (" +
                "\"path\" text, " +
                "\"name\" text, " +
                "\"permissions\" int, " +
                "\"gid\" bigint, " +
                "\"uid\" bigint, " +
                "PRIMARY KEY((path), name));"
            );
            session.Execute(
                $"CREATE TABLE IF NOT EXISTS \"{config.MessageSpaceName}\".\"Files\" (" +
                "\"path\" text, " +
                "\"name\" text, " +
                "\"modified\" timestamp, " +
                "\"extended_attributes\" blob, " +
                "\"data\" blob, " +
                "\"content_guid\" uuid, " +
                "\"permissions\" int, " +
                "\"gid\" bigint, " +
                "\"uid\" bigint, " +
                "PRIMARY KEY((path), name));"
            );
            session.Execute(
                $"CREATE TABLE IF NOT EXISTS \"{config.MessageSpaceName}\".\"FilesContent\" (" +
                "\"guid\" uuid, " +
                "\"data\" blob, " +
                "PRIMARY KEY(guid));"
            );
            logger.Info("Creating tables...complete");
        }

        private static Session ConnectToCassandra(Config config, ILog logger)
        {
            logger.Info($"Connecting to cassandra: {string.Join(", ", config.CassandraEndPoints.Select(x => x.Host))}");
            var endpoints = config
                            .CassandraEndPoints
                            .SelectMany(x => Dns.GetHostAddresses(x.Host))
                            .Select(x => new IPEndPoint(x, 9042))
                            .ToArray();
            logger.Info($"Cassandra endpoints: {string.Join(", ", endpoints.Select(x => x.ToString()))}");
            var cluster = Cluster.Builder().AddContactPoints(endpoints).Build();
            for (var i = 0; i <= config.ConnectionAttemptsCount; i++)
            {
                try
                {
                    var session = (Session)cluster.Connect();
                    logger.Info("Connection established");
                    return session;
                }
                catch (Exception e)
                {
                    Thread.Sleep(config.ReconnectTimeout);
                    Console.WriteLine($"Connection failed ({i}) - {e.Message}");
                }
            }

            logger.Fatal("Can't connect to cassandra");
            throw new OperationCanceledException("Can't connect to cassandra");
        }
    }
}