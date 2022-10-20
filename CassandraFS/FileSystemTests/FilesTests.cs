using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using FluentAssertions;

using NUnit.Framework;

using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;

namespace FileSystemTests
{
    public class FilesTests
    {
        private readonly string mountPoint = "/home/cassandra-fs/";
        private string testDirectory;
        private ILog logger;
        private Stopwatch sw;

        [SetUp]
        public void SetUp()
        {
            logger = new CompositeLog(new ConsoleLog().WithDisabledLevels(LogLevel.Info));
            testDirectory = Path.Combine(mountPoint, Guid.NewGuid().ToString());
            Directory.CreateDirectory(testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(testDirectory, true);
        }

        [Test]
        public void TestWriteValidFile()
        {
            logger.Warn($"starting {nameof(TestWriteValidFile)} test");
            sw = Stopwatch.StartNew();
            var fileName = Guid.NewGuid().ToString();
            var fileContent = Guid.NewGuid().ToString();
            var filePath = Path.Combine(testDirectory, fileName);
            File.WriteAllText(filePath, fileContent, Encoding.UTF8);
            var actualContent = File.ReadAllText(filePath, Encoding.UTF8);
            actualContent.Should().Be(fileContent);
            logger.Warn($"test {nameof(TestWriteValidFile)} passed with elapsed {sw.ElapsedMilliseconds}");
            // var unixFileInfo = new Mono.Unix.UnixFileInfo(filePath);
        }

        [Test]
        public void TestWriteEmptyFile()
        {
            logger.Warn($"starting {nameof(TestWriteEmptyFile)} test");
            sw = Stopwatch.StartNew();
            var fileName = Guid.NewGuid().ToString();
            var filePath = Path.Combine(testDirectory, fileName);
            File.WriteAllText(filePath, "", Encoding.UTF8);
            var actualContent = File.ReadAllText(filePath, Encoding.UTF8);
            actualContent.Should().Be("");
            logger.Warn($"test {nameof(TestWriteEmptyFile)} passed with elapsed {sw.ElapsedMilliseconds}");
        }

        [Test]
        public void TestWriteFileInNonExistingDirectoryReturnsError()
        {
            logger.Warn($"starting {nameof(TestWriteFileInNonExistingDirectoryReturnsError)} test");
            sw = Stopwatch.StartNew();
            var fileName = Guid.NewGuid().ToString();
            var dirName = Guid.NewGuid().ToString();
            var fileContent = Guid.NewGuid().ToString();
            var filePath = Path.Combine(testDirectory, dirName, fileName);
            Assert.Throws<DirectoryNotFoundException>(() => File.WriteAllText(filePath, fileContent, Encoding.UTF8));
            File.Exists(filePath).Should().BeFalse();
            logger.Warn($"test {nameof(TestWriteFileInNonExistingDirectoryReturnsError)} passed with elapsed {sw.ElapsedMilliseconds}");
        }

        [Test]
        public void TestWriteFileInsideOtherFileReturnsError()
        {
            logger.Warn($"starting {nameof(TestWriteFileInsideOtherFileReturnsError)} test");
            sw = Stopwatch.StartNew();
            var fileName = Guid.NewGuid().ToString();
            var parentFileName = Guid.NewGuid().ToString();
            var fileContent = Guid.NewGuid().ToString();
            var parentFilePath = Path.Combine(testDirectory, parentFileName);
            File.WriteAllText(parentFilePath,fileContent, Encoding.UTF8);
            File.Exists(parentFilePath).Should().BeTrue();
            var filePath = Path.Combine(testDirectory, parentFileName, fileName);
            Assert.Throws<IOException>(() => File.WriteAllText(filePath, fileContent, Encoding.UTF8));
            File.Exists(filePath).Should().BeFalse();
            logger.Warn($"test {nameof(TestWriteFileInsideOtherFileReturnsError)} passed with elapsed {sw.ElapsedMilliseconds}");
        }

        [Test]
        public void TestWriteFileWithIncorrectPath()
        {
            logger.Warn($"starting {nameof(TestWriteFileWithIncorrectPath)} test");
            sw = Stopwatch.StartNew();
            var fileName = "..\\test/%3!..0)(=";
            var fileContent = Guid.NewGuid().ToString();
            var filePath = Path.Combine(testDirectory, fileName);
            Assert.Throws<DirectoryNotFoundException>(() => File.WriteAllText(filePath, fileContent, Encoding.UTF8));
            File.Exists(filePath).Should().BeFalse();
            logger.Warn($"test {nameof(TestWriteFileWithIncorrectPath)} passed with elapsed {sw.ElapsedMilliseconds}");
        }

        [Test]
        public void TestRenameFile()
        {
            logger.Warn($"starting {nameof(TestRenameFile)} test");
            sw = Stopwatch.StartNew();
            var fileName = Guid.NewGuid().ToString();
            var fileContent = Guid.NewGuid().ToString();
            var filePath = Path.Combine(testDirectory, fileName);
            File.WriteAllText(filePath,fileContent, Encoding.UTF8);

            var directoryName = Guid.NewGuid().ToString();
            var directoryPath = Path.Combine(testDirectory, directoryName);
            Directory.CreateDirectory(directoryPath);

            var newFileName = Guid.NewGuid().ToString();
            var newFilePath = Path.Combine(directoryPath, newFileName);

            File.Move(filePath, newFilePath);
            File.Exists(filePath).Should().BeFalse();
            File.Exists(newFilePath).Should().BeTrue();

            var actualContent = File.ReadAllText(newFilePath, Encoding.UTF8);
            actualContent.Should().Be(fileContent);
            logger.Warn($"test {nameof(TestRenameFile)} passed with elapsed {sw.ElapsedMilliseconds}");
        }

        [Test]
        public void TestWriteManyFiles()
        {
            logger.Warn($"starting {nameof(TestWriteManyFiles)} test");
            sw = Stopwatch.StartNew();
            var filePaths = new List<string>();
            var fileContents = new List<string>();
            for (var i = 0; i < 50; i++)
            {
                var fileName = Guid.NewGuid().ToString();
                var fileContent = Guid.NewGuid().ToString();
                var filePath = Path.Combine(testDirectory, fileName);
                filePaths.Add(filePath);
                fileContents.Add(fileContent);
                File.WriteAllText(filePath, fileContent, Encoding.UTF8);
            }

            for (var i = 0; i < 50; i++)
            {
                var actualContent = File.ReadAllText(filePaths[i], Encoding.UTF8);
                actualContent.Should().Be(fileContents[i]);
            }
            logger.Warn($"test {nameof(TestWriteManyFiles)} passed with elapsed {sw.ElapsedMilliseconds}");
        }

        [Test]
        public void TestDeleteFile()
        {
            logger.Warn($"starting {nameof(TestDeleteFile)} test");
            sw = Stopwatch.StartNew();
            var fileName = Guid.NewGuid().ToString();
            var fileContent = Guid.NewGuid().ToString();
            var filePath = Path.Combine(testDirectory, fileName);
            File.WriteAllText(filePath, fileContent, Encoding.UTF8);
            File.Exists(filePath).Should().BeTrue();
            File.Delete(filePath);
            File.Exists(filePath).Should().BeFalse();
            logger.Warn($"test {nameof(TestDeleteFile)} passed with elapsed {sw.ElapsedMilliseconds}");
        }

        [Test]
        public void TestChangeFile()
        {
            logger.Warn($"starting {nameof(TestChangeFile)} test");
            sw = Stopwatch.StartNew();
            var fileName = Guid.NewGuid().ToString();
            var fileContent = Guid.NewGuid().ToByteArray();
            var filePath = Path.Combine(testDirectory, fileName);
            File.WriteAllBytes(filePath, fileContent);
            var actualContent = File.ReadAllBytes(filePath);
            actualContent.Should().BeEquivalentTo(fileContent);

            fileContent = new byte[0];
            var file = File.OpenWrite(filePath);
            file.Seek(0, SeekOrigin.Begin);
            file.Write(fileContent);
            file.Close();
            actualContent = File.ReadAllBytes(filePath);
            actualContent.Should().BeEquivalentTo(fileContent);

            fileContent = Guid.NewGuid().ToByteArray();
            file = File.OpenWrite(filePath);
            file.Write(fileContent);
            file.Close();
            actualContent = File.ReadAllBytes(filePath);
            actualContent.Should().BeEquivalentTo(fileContent);
            logger.Warn($"test {nameof(TestChangeFile)} passed with elapsed {sw.ElapsedMilliseconds}");
        }


        [Test]
        [Explicit]
        public void TestWriteBigFile()
        {
            logger.Warn($"starting {nameof(TestWriteBigFile)} test");
            sw = Stopwatch.StartNew();
            var fileName = Guid.NewGuid().ToString();
            var bigData = new List<byte>();
            for (var i = 0; i < 1024*1024*4; i++)
            {
                bigData.AddRange(Guid.NewGuid().ToByteArray());
            }
            var filePath = Path.Combine(testDirectory, fileName);
            File.WriteAllBytes(filePath, bigData.ToArray());
            var actualContent = File.ReadAllBytes(filePath);
            actualContent.Should().BeEquivalentTo(bigData.ToArray());
            logger.Warn($"test {nameof(TestWriteBigFile)} passed with elapsed {sw.ElapsedMilliseconds}");
        }
    }
}