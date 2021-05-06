using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CassandraFS.CassandraHandler;
using CassandraFS.Models;

using Mono.Fuse.NETStandard;
using Mono.Unix.Native;

namespace CassandraFS
{
    public class FileSystemRepository
    {
        private static readonly HashSet<string> rootDirPaths = new HashSet<string> {"", ".", Path.DirectorySeparatorChar.ToString()};
        private readonly FileRepository fileRepository;
        private readonly DirectoryRepository directoryRepository;
        private static readonly string rootDirectoryPath = Path.DirectorySeparatorChar.ToString();

        public FileSystemRepository(FileRepository fileRepository, DirectoryRepository directoryRepository)
        {
            this.directoryRepository = directoryRepository;
            this.fileRepository = fileRepository;
        }

        public List<DirectoryEntry> ReadDirectoryContent(string path) =>
            directoryRepository.ReadDirectoryContent(path)
                               .Concat(fileRepository.ReadDirectoryContent(path))
                               .ToList();

        public bool IsDirectoryEmpty(string path)
            => !(fileRepository.IsFilesExists(path) || directoryRepository.IsDirectoriesExists(path));

        public static string GetParentDirectory(string path)
        {
            path = Path.TrimEndingDirectorySeparator(path);
            var parentDirPath = Path.GetDirectoryName(path);
            if (parentDirPath == null || rootDirPaths.Contains(parentDirPath) || rootDirPaths.Contains(path))
            {
                return rootDirectoryPath;
            }

            return parentDirPath + Path.DirectorySeparatorChar;
        }

        public static string GetFileName(string path)
        {
            path = Path.TrimEndingDirectorySeparator(path);
            return Path.GetFileName(path);
        }

        public Result<DirectoryModel> ReadDirectory(string path)
        {
            return IsDirectoryValid(GetParentDirectory(path))
                   .Check(() => !fileRepository.IsFileExists(path), FileSystemError.NotDirectory)
                   .Then(() => directoryRepository.ReadDirectory(path))
                   .Check(directory => directory != null, FileSystemError.NoEntry);
        }

        public Result WriteDirectory(string path, FilePermissions mode)
        {
            var parentDirPath = GetParentDirectory(path);
            return IsDirectoryValid(parentDirPath)
                   .Check(() => !directoryRepository.IsDirectoryExists(path), FileSystemError.AlreadyExist)
                   .Then(() =>
                       {
                           var uid = Syscall.getuid();
                           var gid = Syscall.getgid();
                           mode |= FilePermissions.S_IFDIR;
                           directoryRepository.WriteDirectory(new DirectoryModel
                               {
                                   Path = parentDirPath,
                                   Name = GetFileName(path),
                                   FilePermissions = mode,
                                   UID = uid,
                                   GID = gid,
                                   ModifiedTimestamp = DateTimeOffset.Now
                               });
                           return Result.Ok();
                       });
        }

        public Result DeleteDirectory(string path)
        {
            return IsDirectoryValid(path)
                   .Check(() => IsDirectoryEmpty(path), FileSystemError.DirectoryNotEmpty)
                   .Then(() =>
                       {
                           directoryRepository.DeleteDirectory(path);
                           return Result.Ok();
                       });
        }

        public Result RenameDirectory(string from, string to)
        {
            return ReadDirectory(from)
                   .Check(fromDirectory => !to.StartsWith(from), FileSystemError.InvalidArgument)
                   .Check(fromDirectory => !directoryRepository.IsDirectoryExists(to), FileSystemError.AlreadyExist)
                   .Then(fromDirectory =>
                       {
                           directoryRepository.DeleteDirectory(from);
                           fromDirectory.Name = GetFileName(to);
                           fromDirectory.Path = GetParentDirectory(to);
                           fromDirectory.ModifiedTimestamp = DateTimeOffset.Now;
                           directoryRepository.WriteDirectory(fromDirectory);

                           GetTree(fromDirectory, out var directoriesToRename, out var filesToRename);
                           foreach (var file in filesToRename)
                           {
                               var filePath = file.Path + file.Name;
                               fileRepository.RenameFile(filePath, file.Path.Replace(from, to) + file.Name);
                           }
                           foreach (var directory in directoriesToRename)
                           {
                               var directoryPath = directory.Path + directory.Name;
                               directoryRepository.DeleteDirectory(directoryPath);
                               directory.ModifiedTimestamp = DateTimeOffset.Now;
                               directory.Path = directory.Path.Replace(from, to);
                               directoryRepository.WriteDirectory(directory);
                           }
                           return Result.Ok();
                       });
        }

        public Result<FileModel> ReadFile(string path, OpenFlags flags)
        {
            // O_APPEND not implemented
            var directoryCheck = CheckFileParentDirectory(path);
            if (!directoryCheck.IsSuccessful())
            {
                return directoryCheck;
            }

            var file = fileRepository.ReadFile(path);

            switch (file == null)
            {
            case true when (flags & OpenFlags.O_CREAT) == 0:
                return Result.Fail(FileSystemError.NoEntry);
            case true when (flags & OpenFlags.O_CREAT) != 0:
                var fileName = GetFileName(path);
                var parentDirPath = GetParentDirectory(path);
                var now = DateTimeOffset.Now;
                var euid = Syscall.geteuid();
                var egid = Syscall.getegid();
                file = new FileModel
                    {
                        Path = parentDirPath,
                        Name = fileName,
                        Data = new byte[0],
                        ModifiedTimestamp = now,
                        ExtendedAttributes = new ExtendedAttributes(),
                        FilePermissions = FilePermissions.ACCESSPERMS | FilePermissions.S_IFREG,
                        GID = egid,
                        UID = euid,
                    };
                fileRepository.WriteFile(file);
                return Result.Ok(file);
            case false when (flags & OpenFlags.O_TRUNC) != 0:
                file.Data = new byte[0];
                return Result.Ok(file);
            default:
                return Result.Ok(file);
            }
        }

        private Result CheckFileParentDirectory(string path)
        {
            return IsDirectoryValid(GetParentDirectory(path))
                .Check(() => !directoryRepository.IsDirectoryExists(path), FileSystemError.IsDirectory);
        }

        public Result<FileModel> ReadFile(string path)
        {
            return CheckFileParentDirectory(path)
                   .Then(() => fileRepository.ReadFile(path))
                   .Check(file => file != null, FileSystemError.NoEntry);
        }

        public void WriteFile(FileModel file)
        {
            fileRepository.WriteFile(file);
        }

        public Result CreateFile(string path, FilePermissions mode, ulong rdev)
        {
            var parentDirPath = GetParentDirectory(path);
            return IsDirectoryValid(parentDirPath)
                   .Check(() => !fileRepository.IsFileExists(path), FileSystemError.AlreadyExist)
                   .Then(() =>
                       {
                           var fileName = GetFileName(path);
                           var now = DateTimeOffset.Now;
                           var uid = Syscall.getuid();
                           var gid = Syscall.getgid();
                           var file = new FileModel
                               {
                                   Path = parentDirPath,
                                   Name = fileName,
                                   Data = new byte[0],
                                   ExtendedAttributes = new ExtendedAttributes(),
                                   ModifiedTimestamp = now,
                                   FilePermissions = mode,
                                   GID = gid,
                                   UID = uid,
                               };
                           fileRepository.WriteFile(file);
                           return Result.Ok();
                       });
        }

        public Result DeleteFile(string path)
        {
            return IsDirectoryValid(GetParentDirectory(path))
                   .Check(() => fileRepository.IsFileExists(path), FileSystemError.NoEntry)
                   .Then(() =>
                       {
                           fileRepository.DeleteFile(path);
                           return Result.Ok();
                       });
        }

        public Result RenameFile(string from, string to)
        {
            return Result.Ok()
                   .Check(() => !directoryRepository.IsDirectoryExists(to), FileSystemError.IsDirectory)
                   .Check(() => !fileRepository.IsFileExists(to), FileSystemError.AlreadyExist)
                   .Then(() =>
                       {
                           fileRepository.RenameFile(from, to);
                           return Result.Ok();
                       });
        }

        public Result SetExtendedAttribute(string path, string name, byte[] value, XattrFlags flags)
        {
            return ReadFile(path)
                .Then(file =>
                    {
                        var attributes = file.ExtendedAttributes.Attributes;
                        if (attributes.ContainsKey(name) && flags == XattrFlags.XATTR_CREATE)
                        {
                            return Result.Fail(FileSystemError.AlreadyExist);
                        }

                        if (!attributes.ContainsKey(name) && flags == XattrFlags.XATTR_REPLACE)
                        {
                            return Result.Fail(FileSystemError.NoAttribute);
                        }

                        attributes.Add(name, value);
                        WriteFile(file);
                        return Result.Ok();
                    });
        }

        public Result<int> GetExtendedAttribute(string path, string name, byte[] value)
        {
            return ReadFile(path)
                .Then(file =>
                    {
                        var attributes = file.ExtendedAttributes.Attributes;
                        if (!attributes.ContainsKey(name))
                        {
                            return Result.Fail(FileSystemError.NoAttribute);
                        }

                        var attribute = attributes[name];
                        if (attribute.Length > value.Length)
                        {
                            return Result.Fail(FileSystemError.OutOfRange);
                        }

                        using var ms = new MemoryStream(value);
                        ms.Write(attribute);
                        return Result.Ok(attribute.Length);
                    });
        }

        public Result<string[]> GetExtendedAttributesList(string path)
        {
            return ReadFile(path)
                .Then(file => Result.Ok(file.ExtendedAttributes.Attributes.Keys.ToArray()));
        }

        public Result RemoveExtendedAttribute(string path, string name)
        {
            return ReadFile(path)
                .Then(file =>
                    {
                        var attributes = file.ExtendedAttributes.Attributes;
                        if (!attributes.ContainsKey(name))
                        {
                            return Result.Fail(FileSystemError.NoAttribute);
                        }
                        attributes.Remove(name);
                        WriteFile(file);
                        return Result.Ok();
                    });
        }

        public Result<Stat> GetPathStatus(string path)
        {
            return GetFileSystemEntry(path)
                .Then(entry => Result.Ok(entry.GetStat()));
        }

        public Result ChangePathPermissions(string path, FilePermissions permissions)
        {
            return GetFileSystemEntry(path)
                .Then(entry =>
                    {
                        var euid = Syscall.geteuid();
                        if (euid != 0 && entry.UID != euid)
                        {
                            return Result.Fail(FileSystemError.PermissionDenied);
                        }
                        WriteFileSystemEntry(entry);
                        return Result.Ok();
                    });
        }

        public Result ChangePathOwner(string path, uint newUID, uint newGID)
        {
            return GetFileSystemEntry(path)
                .Then(entry =>
                    {
                        if (!entry.IsChownPermissionsOk(newUID, newGID))
                        {
                            return Result.Fail(FileSystemError.PermissionDenied);
                        }
                        entry.UID = newUID;
                        entry.GID = newGID;
                        WriteFileSystemEntry(entry);
                        return Result.Ok();
                    });
        }

        public Result GetAccessToPath(string path, AccessModes mode)
        {
            return GetFileSystemEntry(path)
                .Then(entry =>
                    {
                        var permissions = entry.FilePermissions;
                        var fileUID = entry.UID;
                        var fileGID = entry.GID;
                        var userUID = Syscall.getuid();
                        var userGID = Syscall.getgid();
                        if (((AccessModes.R_OK & mode) != 0 && !permissions.CanUserRead(userUID, userGID, fileUID, fileGID))
                            || ((AccessModes.W_OK & mode) != 0 && !permissions.CanUserWrite(userUID, userGID, fileUID, fileGID))
                            || ((AccessModes.X_OK & mode) != 0 && !permissions.CanUserExecute(userUID, userGID, fileUID, fileGID)))
                        {
                            return Result.Fail(FileSystemError.AccessDenied);
                        }
                        return Result.Ok();
                    });
        }

        public Result<Statvfs> GetFileSystemStatus(string path)
        {
            return GetFileSystemEntry(path)
                .Then(entry => Result.Ok(new Statvfs
                    {
                        f_bsize = 4096,
                        f_frsize = 4096,
                        f_blocks = 1, // just not zero
                        f_bfree = ulong.MaxValue,
                        f_bavail = ulong.MaxValue,
                        f_files = 4096, // Maybe it should be valid counter
                        f_ffree = ulong.MaxValue,
                        f_favail = ulong.MaxValue,
                        f_fsid = 1,
                        f_namemax = 255
                    }));
        }

        private Result<IFileSystemEntry> GetFileSystemEntry(string path)
        {
            var parentDirectoryCheck = IsDirectoryValid(GetParentDirectory(path));
            if (!parentDirectoryCheck.IsSuccessful())
            {
                return parentDirectoryCheck;
            }

            var file = ReadFile(path);
            if (file.IsSuccessful())
            {
                return Result.Ok(file.Value as IFileSystemEntry);
            }
            var directory = ReadDirectory(path);
            if (directory.IsSuccessful())
            {
                return Result.Ok(directory.Value as IFileSystemEntry);
            }
            return Result.Fail(directory.ErrorType);
        }

        private void WriteFileSystemEntry(IFileSystemEntry entry)
        {
            switch (entry)
            {
            case FileModel model:
                fileRepository.WriteFile(model);
                return;
            case DirectoryModel model:
                directoryRepository.WriteDirectory(model);
                return;
            default:
                throw new NotImplementedException($"Unsupported FileSystemEntry type: {entry}");
            }
        }

        private Result IsDirectoryValid(string directory)
        {
            return directoryRepository.IsDirectoryExists(directory)
                       ? Result.Ok()
                       : Result.Fail(fileRepository.IsFileExists(directory) ? FileSystemError.NotDirectory : FileSystemError.NoEntry);
        }

        private void GetTree(DirectoryModel rootDirectory, out List<DirectoryModel> directories, out List<FileModel> files)
        {
            var directoriesToVisit = new Queue<DirectoryModel>();
            directoriesToVisit.Enqueue(rootDirectory);
            directories = new List<DirectoryModel>();
            files = new List<FileModel>();
            while (directoriesToVisit.Count != 0)
            {
                var currentDirectory = directoriesToVisit.Dequeue();
                var currentDirectoryPath = currentDirectory.Path + currentDirectory.Name;
                var childDirectories = directoryRepository.GetChildDirectories(currentDirectoryPath);
                foreach (var directory in childDirectories)
                {
                    directoriesToVisit.Enqueue(directory);
                    directories.Add(directory);
                }
                var childFiles = fileRepository.GetChildFiles(currentDirectoryPath);
                files.AddRange(childFiles);
            }
        }
    }
}