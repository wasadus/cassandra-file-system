using System;
using System.IO;
using System.Linq;

using FluentAssertions;

using MoreLinq;

using NUnit.Framework;

namespace StorageTests
{
    public class BigFilesTests : DefaultSettingsTestsBase
    {
        [Test]
        public void TestWriteBigFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = GetTestBigFileData();

            var cqlFile = GetCQLFileFromFileModel(file);
            cqlFile.Data = new byte[0];

            fileRepository.WriteFile(file);
            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);
            CompareFileModel(file, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }

        [Test]
        public void TestDeleteBigFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = GetTestBigFileData();

            var cqlFile = GetCQLFileFromFileModel(file);
            cqlFile.Data = new byte[0];

            fileRepository.WriteFile(file);
            fileRepository.IsFileExists(file.Path + file.Name).Should().BeTrue();
            ReadCQLFile(file.Path, file.Name).Should().NotBeNull();
            fileRepository.DeleteFile(file.Path + file.Name);
            fileRepository.IsFileExists(file.Path + file.Name).Should().BeFalse();
            ReadCQLFile(file.Path, file.Name).Should().BeNull();
        }

        [Test]
        public void TestReplaceBigFileWithSmallFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = GetTestBigFileData();

            fileRepository.WriteFile(file);
            var bigFile = fileRepository.ReadFile(file.Path + file.Name);

            bigFile.Data = Guid.NewGuid().ToByteArray();
            fileRepository.WriteFile(bigFile);
            var cqlFile = GetCQLFileFromFileModel(bigFile);

            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);

            CompareFileModel(bigFile, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }

        [Test]
        public void TestReplaceSmallFileWithBigFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            fileRepository.WriteFile(file);

            var smallFile = fileRepository.ReadFile(file.Path + file.Name);
            smallFile.Data = GetTestBigFileData();
            fileRepository.WriteFile(smallFile);

            var cqlFile = GetCQLFileFromFileModel(smallFile);
            cqlFile.Data = new byte[0];

            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);

            CompareFileModel(smallFile, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }
        [Test]
        public void TestReplaceBigFileWithBigFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = GetTestBigFileData();
            fileRepository.WriteFile(file);

            var smallFile = fileRepository.ReadFile(file.Path + file.Name);
            smallFile.Data = GetTestBigFileData().Concat(GetTestBigFileData()).ToArray();
            fileRepository.WriteFile(smallFile);

            var cqlFile = GetCQLFileFromFileModel(smallFile);
            cqlFile.Data = new byte[0];

            var actualFile = fileRepository.ReadFile(file.Path + file.Name);
            var actualCQLFile = ReadCQLFile(file.Path, file.Name);

            CompareFileModel(smallFile, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }

        [Test]
        public void TestRenameBigFile()
        {
            var file = GetTestFileModel(Path.DirectorySeparatorChar.ToString());
            file.Data = GetTestBigFileData();

            var cqlFile = GetCQLFileFromFileModel(file);
            cqlFile.Data = new byte[0];
            
            fileRepository.WriteFile(file);
            var newFileName = Guid.NewGuid().ToString();
            fileRepository.RenameFile(file.Path + file.Name, file.Path + newFileName);
            fileRepository.IsFileExists(Path.Combine(file.Path, file.Name));

            var actualFile = fileRepository.ReadFile(file.Path + newFileName);
            var actualCQLFile = ReadCQLFile(file.Path, newFileName);
            file.Name = newFileName;
            cqlFile.Name = newFileName;
            file.ModifiedTimestamp = actualFile.ModifiedTimestamp;
            cqlFile.ModifiedTimestamp = actualCQLFile.ModifiedTimestamp;
            CompareFileModel(file, actualFile);
            CompareCQLFile(cqlFile, actualCQLFile);
        }
    }
}