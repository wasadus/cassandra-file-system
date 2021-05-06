using System;
using System.Collections.Generic;
using System.IO;

using Cassandra;
using Cassandra.Data.Linq;

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
    public class DefaultSettingsTestsBase
    {
        public DirectoryRepository directoryRepository;
        public FileRepository fileRepository;
        public Table<CQLDirectory> directoriesTableEvent;
        public Table<CQLFile> filesTableEvent;

        public virtual Config Config =>
            new Config
                {
                    CassandraEndPoints = new List<NodeSettings>
                        {
                            new NodeSettings {Host = "127.0.0.1"}
                        },
                    MessageSpaceName = "FTPMessageSpace",
                    DropFilesTable = true,
                    DropDirectoriesTable = true,
                    DropFilesContentMetaTable = true,
                    DropFilesContentTable = true,
                    DefaultDataBufferSize = 128,
                    DefaultTTL = 60,
                    ConnectionAttemptsCount = 5,
                    ReconnectTimeout = 5000
                };

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var configuration = new ContainerConfiguration(typeof(CassandraFileSystem).Assembly);
            var container = new Container(configuration);
            var settings = new FileLogSettings();
            var logger = new CompositeLog(new ConsoleLog(), new FileLog(settings));
            container.Configurator.ForAbstraction<ILog>().UseInstances(logger);
            container.Configurator.ForAbstraction<Config>().UseInstances(Config);
            CassandraConfigurator.ConfigureCassandra(container, logger);
            directoryRepository = container.Get<DirectoryRepository>();
            fileRepository = container.Get<FileRepository>();
            var session = container.Get<ISession>();
            directoriesTableEvent = new Table<CQLDirectory>(session);
            filesTableEvent = new Table<CQLFile>(session);
        }

        public CQLDirectory GetCQLDirectoryFromDirectoryModel(DirectoryModel directory)
            => new CQLDirectory
                {
                    Path = directory.Path,
                    Name = directory.Name,
                    FilePermissions = (int)directory.FilePermissions,
                    GID = directory.GID,
                    UID = directory.UID,
                    ModifiedTimestamp = directory.ModifiedTimestamp
                };

        public CQLDirectory ReadCQLDirectory(string path, string name)
            => directoriesTableEvent.FirstOrDefault(x => x.Path == path && x.Name == name).Execute();

        public CQLFile GetCQLFileFromFileModel(FileModel file)
            => new CQLFile
                {
                    Path = file.Path,
                    Name = file.Name,
                    ExtendedAttributes = FileExtendedAttributesHandler.SerializeExtendedAttributes(file.ExtendedAttributes),
                    ModifiedTimestamp = file.ModifiedTimestamp,
                    FilePermissions = (int)file.FilePermissions,
                    GID = file.GID,
                    UID = file.UID,
                    Data = file.Data
                };

        public CQLFile ReadCQLFile(string path, string name)
            => filesTableEvent.FirstOrDefault(x => x.Path == path && x.Name == name).Execute();

        public DirectoryModel GetTestDirectoryModel(string path, FilePermissions permissions = FilePermissions.S_IFDIR, uint gid = 0, uint uid = 0)
        {
            path = path == Path.DirectorySeparatorChar.ToString() ? path : path + Path.DirectorySeparatorChar;
            return new DirectoryModel()
                {
                    Name = Guid.NewGuid().ToString(),
                    Path = path,
                    FilePermissions = FilePermissions.S_IFDIR | permissions,
                    GID = gid,
                    UID = uid,
                    ModifiedTimestamp = DateTimeOffset.Now
                };
        }

        public void CompareDirectoryModel(DirectoryModel expected, DirectoryModel actual)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            expected.Should().NotBeNull();
            actual.Should().NotBeNull();
            actual.Name.Should().Be(expected.Name);
            actual.Path.Should().Be(expected.Path);
            actual.GID.Should().Be(expected.GID);
            actual.UID.Should().Be(expected.UID);
            actual.ModifiedTimestamp.Should().BeCloseTo(expected.ModifiedTimestamp);
            actual.FilePermissions.Should().HaveFlag(expected.FilePermissions);
        }

        public void CompareDirectoryStat(Stat expected, Stat actual)
        {
            actual.st_mtim.tv_sec.Should().BeCloseTo(expected.st_mtim.tv_sec, 1000);
            actual.st_mtim.tv_nsec.Should().BeCloseTo(expected.st_mtim.tv_nsec, 1000000);
            actual.st_mode.Should().HaveFlag(expected.st_mode);
            actual.st_nlink.Should().BeGreaterOrEqualTo(expected.st_nlink);
            actual.st_size.Should().BeGreaterOrEqualTo(expected.st_size);
            actual.st_gid.Should().Be(expected.st_gid);
            actual.st_uid.Should().Be(expected.st_uid);
        }

        public void CompareCQLDirectory(CQLDirectory expected, CQLDirectory actual)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            expected.Should().NotBeNull();
            actual.Should().NotBeNull();
            actual.Name.Should().Be(expected.Name);
            actual.Path.Should().Be(expected.Path);
            actual.GID.Should().Be(expected.GID);
            actual.UID.Should().Be(expected.UID);
            actual.ModifiedTimestamp.Should().BeCloseTo(expected.ModifiedTimestamp);
            actual.FilePermissions.Should().BeGreaterOrEqualTo(expected.FilePermissions);
        }

        public FileModel GetTestFileModel(string path, FilePermissions permissions = FilePermissions.S_IFDIR, uint gid = 0, uint uid = 0)
        {
            path = path == Path.DirectorySeparatorChar.ToString() ? path : path + Path.DirectorySeparatorChar;
            return new FileModel
                {
                    Name = Guid.NewGuid().ToString(),
                    Path = path,
                    FilePermissions = FilePermissions.S_IFREG | permissions,
                    GID = gid,
                    UID = uid,
                    ModifiedTimestamp = DateTimeOffset.Now,
                    Data = Guid.NewGuid().ToByteArray(),
                    ExtendedAttributes = new ExtendedAttributes
                        {
                            Attributes = new Dictionary<string, byte[]> {{Guid.NewGuid().ToString(), Guid.NewGuid().ToByteArray()}}
                        }
                };
        }

        public void CompareFileModel(FileModel expected, FileModel actual)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            expected.Should().NotBeNull();
            actual.Should().NotBeNull();
            actual.Name.Should().Be(expected.Name);
            actual.Path.Should().Be(expected.Path);
            actual.GID.Should().Be(expected.GID);
            actual.UID.Should().Be(expected.UID);
            actual.ModifiedTimestamp.Should().BeCloseTo(expected.ModifiedTimestamp);
            actual.FilePermissions.Should().HaveFlag(expected.FilePermissions);
            if (actual.Data != null && expected.Data != null)
            {
                actual.Data.Should().BeEquivalentTo(expected.Data);
            }
            else
            {
                actual.Data.Should().BeNull();
                expected.Data.Should().BeNull();
            }
            actual.ExtendedAttributes.Attributes.Keys.Should().BeEquivalentTo(expected.ExtendedAttributes.Attributes.Keys);
            actual.ExtendedAttributes.Attributes.Values.Should().BeEquivalentTo(expected.ExtendedAttributes.Attributes.Values);
        }

        public void CompareFileStat(Stat expected, Stat actual)
        {
            actual.st_mtim.tv_sec.Should().BeCloseTo(expected.st_mtim.tv_sec, 1000);
            actual.st_mtim.tv_nsec.Should().BeCloseTo(expected.st_mtim.tv_nsec, 1000000);
            actual.st_mode.Should().HaveFlag(expected.st_mode);
            actual.st_nlink.Should().BeGreaterOrEqualTo(expected.st_nlink);
            actual.st_size.Should().BeGreaterOrEqualTo(expected.st_size);
            actual.st_gid.Should().Be(expected.st_gid);
            actual.st_uid.Should().Be(expected.st_uid);
            actual.st_size.Should().BeGreaterOrEqualTo(expected.st_size);
            actual.st_blocks.Should().BeGreaterOrEqualTo(expected.st_blocks);
        }

        public void CompareCQLFile(CQLFile expected, CQLFile actual)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            expected.Should().NotBeNull();
            actual.Should().NotBeNull();
            actual.Name.Should().Be(expected.Name);
            actual.Path.Should().Be(expected.Path);
            actual.GID.Should().Be(expected.GID);
            actual.UID.Should().Be(expected.UID);
            actual.ModifiedTimestamp.Should().BeCloseTo(expected.ModifiedTimestamp);
            actual.FilePermissions.Should().BeGreaterOrEqualTo(expected.FilePermissions);
            if (actual.Data != null && expected.Data != null)
            {
                actual.Data.Should().BeEquivalentTo(expected.Data);
            }
            else
            {
                actual.Data.Should().BeNullOrEmpty();
                expected.Data.Should().BeNullOrEmpty();
            }
            actual.ExtendedAttributes.Should().BeEquivalentTo(expected.ExtendedAttributes);
        }

        public byte[] GetTestBigFileData()
        {
            var data = new List<byte>();
            for (var i = 0; i < 64; i++)
            {
                data.AddRange(Guid.NewGuid().ToByteArray());
            }
            return data.ToArray();
        }
    }
}