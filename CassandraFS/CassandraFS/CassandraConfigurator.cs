using System;
using System.Linq;
using System.Net;
using System.Threading;

using Cassandra;

using GroboContainer.Core;

using Vostok.Logging.Abstractions;

namespace CassandraFS
{
    public static class CassandraConfigurator
    {
        public static void ConfigureCassandra(IContainer container, ILog logger)
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

            if (config.DropFilesContentMetaTable)
            {
                logger.Info("Dropping files content meta");
                session.Execute($"DROP TABLE IF EXISTS \"{config.MessageSpaceName}\".\"FilesContentMeta\";");
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
                "\"modified\" timestamp, " +
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
                "\"blob_version\" uuid, " +
                "\"chunk_id\" smallint, " +
                "\"chunk_bytes\" blob, " +
                "PRIMARY KEY(blob_version, chunk_id));"
            );
            session.Execute(
                $"CREATE TABLE IF NOT EXISTS \"{config.MessageSpaceName}\".\"FilesContentMeta\" (" +
                "\"blob_id\" text, " +
                "\"blob_version\" uuid, " +
                "\"chunks_count\" smallint, " +
                "PRIMARY KEY(blob_id));"
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