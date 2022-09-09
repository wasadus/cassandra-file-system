using System.Collections.Generic;
using System.IO;
using System.Threading;

using CassandraFS;

using FluentAssertions;

using NUnit.Framework;

namespace StorageTests
{
    public class TTLTests : DefaultSettingsTestsBase
    {
        public override Config Config =>
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
                    DefaultTTL = 3,
                    ConnectionAttemptsCount = 5,
                    ReconnectTimeout = 5000
                };

        [Test]
        public void TestFileDoesNotExistAfterTTLExpired()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            fileRepository.WriteFile(file);
            Thread.Sleep(4000);
            var actualFile = fileRepository.ReadFile(Path.Combine(file.Path, file.Name));
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);
            actualCQLFile.Should().BeNull();
            actualFile.Should().BeNull();
            fileRepository.IsFileExists(Path.Combine(file.Path, file.Name)).Should().BeFalse();
        }

        [Test]
        public void TestBigFileDoesNotExistAfterTTLExpired()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = GetTestBigFileData();
            fileRepository.WriteFile(file);
            Thread.Sleep(4000);
            var actualFile = fileRepository.ReadFile(Path.Combine(file.Path, file.Name));
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);
            actualCQLFile.Should().BeNull();
            actualFile.Should().BeNull();
            fileRepository.IsFileExists(Path.Combine(file.Path,file.Name)).Should().BeFalse();
        }
    }
}