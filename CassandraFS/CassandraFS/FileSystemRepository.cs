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
        private static readonly HashSet<string> rootDirPaths = new HashSet<string> {"", "."};
        private readonly FileRepository fileRepository;
        private readonly DirectoryRepository directoryRepository;

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
                parentDirPath = "/";
            }

            return parentDirPath;
        }

        public static string GetFileName(string path)
        {
            path = Path.TrimEndingDirectorySeparator(path);
            return Path.GetFileName(path);
        }

        public Result<DirectoryModel> ReadDirectory(string path)
        {
            var directoryCheck = IsDirectoryValid(GetParentDirectory(path));
            if (!directoryCheck.IsSuccessful())
            {
                return directoryCheck;
            }

            if (fileRepository.IsFileExists(path))
            {
                return Result.Fail(FileSystemError.NotDirectory);
            }

            var directory = directoryRepository.ReadDirectory(path);
            return directory == null ? Result.Fail(FileSystemError.NoEntry) : Result.Ok(directory);
        }

        public Result WriteDirectory(string path, FilePermissions mode)
        {
            var dirName = GetFileName(path);
            var parentDirPath = GetParentDirectory(path);
            var directoryCheck = IsDirectoryValid(parentDirPath);

            if (!directoryCheck.IsSuccessful())
            {
                return directoryCheck;
            }

            if (directoryRepository.IsDirectoryExists(path))
            {
                return Result.Fail(FileSystemError.AlreadyExist);
            }

            var uid = Syscall.getuid();
            var gid = Syscall.getgid();
            mode |= FilePermissions.S_IFDIR;
            directoryRepository.WriteDirectory(new DirectoryModel
                {
                    Path = parentDirPath,
                    Name = dirName,
                    FilePermissions = mode,
                    UID = uid,
                    GID = gid,
                    ModifiedTimestamp = DateTimeOffset.Now
                });
            return Result.Ok();
        }

        public Result DeleteDirectory(string path)
        {
            var directoryCheck = IsDirectoryValid(path);
            if (!directoryCheck.IsSuccessful())
            {
                return directoryCheck;
            }

            if (!IsDirectoryEmpty(path))
            {
                return Result.Fail(FileSystemError.DirectoryNotEmpty);
            }

            directoryRepository.DeleteDirectory(path);
            return Result.Ok();
        }

        public Result RenameDirectory(string from, string to)
        {
            var fromDirectory = ReadDirectory(from);

            if (!fromDirectory.IsSuccessful())
            {
                return Result.Fail(fromDirectory.ErrorType.Value);
            }

            if (to.StartsWith(from))
            {
                return Result.Fail(FileSystemError.InvalidArgument);
            }

            if (directoryRepository.IsDirectoryExists(to))
            {
                return Result.Fail(FileSystemError.AlreadyExist);
            }

            // TODO надо файлы тоже перенести
            var parentDirPath = GetParentDirectory(to);
            var dirName = GetFileName(to);
            directoryRepository.WriteDirectory(new DirectoryModel
                {
                    Path = parentDirPath,
                    Name = dirName,
                    FilePermissions = fromDirectory.Value.FilePermissions,
                    UID = fromDirectory.Value.UID,
                    GID = fromDirectory.Value.GID,
                    ModifiedTimestamp = DateTimeOffset.Now
                });
            directoryRepository.DeleteDirectory(from);
            return Result.Ok();
        }

        public Result<FileModel> ReadFile(string path, OpenFlags flags)
        {
            // O_APPEND not implemented
            var fileName = GetFileName(path);
            var parentDirPath = GetParentDirectory(path);
            var directoryCheck = IsDirectoryValid(parentDirPath);
            if (!directoryCheck.IsSuccessful())
            {
                return directoryCheck;
            }

            if (directoryRepository.IsDirectoryExists(path))
            {
                return Result.Fail(FileSystemError.IsDirectory);
            }

            var file = fileRepository.ReadFile(path);

            if (file != null)
            {
                if ((flags & OpenFlags.O_TRUNC) != 0)
                {
                    file.Data = new byte[0];
                }
                return Result.Ok(file);
            }

            if ((flags & OpenFlags.O_CREAT) == 0)
            {
                return Result.Fail(FileSystemError.NoEntry);
            }

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
        }

        public void WriteFile(FileModel file)
        {
            fileRepository.WriteFile(file);
        }

        public Result CreateFile(string path, FilePermissions mode, ulong rdev)
        {
            var fileName = GetFileName(path);
            var parentDirPath = GetParentDirectory(path);
            var directoryCheck = IsDirectoryValid(parentDirPath);

            if (!directoryCheck.IsSuccessful())
            {
                return directoryCheck;
            }

            if (fileRepository.IsFileExists(path))
            {
                return Result.Fail(FileSystemError.AlreadyExist);
            }

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
        }

        public Result DeleteFile(string path)
        {
            var directoryCheck = IsDirectoryValid(GetParentDirectory(path));
            if (!directoryCheck.IsSuccessful())
            {
                return directoryCheck;
            }

            if (!fileRepository.IsFileExists(path))
            {
                return Result.Fail(FileSystemError.NoEntry);
            }

            fileRepository.DeleteFile(path);
            return Result.Ok();
        }

        public Result RenameFile(string from, string to)
        {
            var file = ReadFile(from, 0);
            if (!file.IsSuccessful())
            {
                return Result.Fail(file.ErrorType.Value);
            }

            if (directoryRepository.IsDirectoryExists(to))
            {
                return Result.Fail(FileSystemError.IsDirectory);
            }

            if (fileRepository.IsFileExists(to))
            {
                return Result.Fail(FileSystemError.AlreadyExist);
            }

            fileRepository.DeleteFile(from);
            var parentDirPath = GetParentDirectory(to);
            var fileName = GetFileName(to);
            file.Value.Name = fileName;
            file.Value.Path = parentDirPath;
            file.Value.ModifiedTimestamp = DateTimeOffset.Now;
            fileRepository.WriteFile(file.Value);
            return Result.Ok();
        }

        public Result SetExtendedAttribute(string path, string name, byte[] value, XattrFlags flags)
        {
            var file = ReadFile(path, 0);
            if (!file.IsSuccessful())
            {
                return Result.Fail(file.ErrorType.Value);
            }

            var attributes = file.Value.ExtendedAttributes.Attributes;
            if (attributes.ContainsKey(name) && flags == XattrFlags.XATTR_CREATE)
            {
                return Result.Fail(FileSystemError.AlreadyExist);
            }

            if (!attributes.ContainsKey(name) && flags == XattrFlags.XATTR_REPLACE)
            {
                return Result.Fail(FileSystemError.NoAttribute);
            }

            attributes.Add(name, value);
            WriteFile(file.Value);
            return Result.Ok();
        }

        public Result<int> GetExtendedAttribute(string path, string name, byte[] value)
        {
            var file = ReadFile(path, 0);
            if (!file.IsSuccessful())
            {
                return Result.Fail(file.ErrorType.Value);
            }

            var attributes = file.Value.ExtendedAttributes.Attributes;
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
        }

        public Result<string[]> GetExtendedAttributesList(string path)
        {
            var file = ReadFile(path, 0);
            if (!file.IsSuccessful())
            {
                return Result.Fail(file.ErrorType.Value);
            }
            return Result.Ok(file.Value.ExtendedAttributes.Attributes.Keys.ToArray());
        }

        public Result RemoveExtendedAttribute(string path, string name)
        {
            var file = ReadFile(path, 0);
            if (!file.IsSuccessful())
            {
                return Result.Fail(file.ErrorType.Value);
            }

            var attributes = file.Value.ExtendedAttributes.Attributes;
            if (!attributes.ContainsKey(name))
            {
                return Result.Fail(FileSystemError.NoAttribute);
            }

            attributes.Remove(name);
            WriteFile(file.Value);
            return Result.Ok();
        }

        public Result<Stat> GetPathStatus(string path)
        {
            var entry = GetFileSystemEntry(path);
            if (!entry.IsSuccessful())
            {
                return Result.Fail(entry.ErrorType.Value);
            }
            return Result.Ok(entry.Value.GetStat());
        }

        public Result ChangePathPermissions(string path, FilePermissions permissions)
        {
            var entry = GetFileSystemEntry(path);
            if (!entry.IsSuccessful())
            {
                return Result.Fail(entry.ErrorType.Value);
            }

            var euid = Syscall.geteuid();
            if (euid != 0 && entry.Value.UID != euid)
            {
                return Result.Fail(FileSystemError.PermissionDenied);
            }

            WriteFileSystemEntry(entry.Value);
            return Result.Ok();
        }

        public Result ChangePathOwner(string path, uint newUID, uint newGID)
        {
            var entry = GetFileSystemEntry(path);
            if (!entry.IsSuccessful())
            {
                return Result.Fail(entry.ErrorType.Value);
            }

            if (!entry.Value.IsChownPermissionsOk(newUID, newGID))
            {
                return Result.Fail(FileSystemError.PermissionDenied);
            }
            entry.Value.UID = newUID;
            entry.Value.GID = newGID;

            WriteFileSystemEntry(entry.Value);
            return Result.Ok();
        }

        public Result GetAccessToPath(string path, AccessModes mode)
        {
            var entry = GetFileSystemEntry(path);
            if (!entry.IsSuccessful())
            {
                return Result.Fail(entry.ErrorType.Value);
            }

            var permissions = entry.Value.FilePermissions;
            var fileUID = entry.Value.UID;
            var fileGID = entry.Value.GID;

            var userUID = Syscall.getuid();
            var userGID = Syscall.getgid();
            if (((AccessModes.R_OK & mode) != 0 && !permissions.CanUserRead(userUID, userGID, fileUID, fileGID))
                || ((AccessModes.W_OK & mode) != 0 && !permissions.CanUserWrite(userUID, userGID, fileUID, fileGID))
                || ((AccessModes.X_OK & mode) != 0 && !permissions.CanUserExecute(userUID, userGID, fileUID, fileGID)))
            {
                return Result.Fail(FileSystemError.AccessDenied);
            }
            return Result.Ok();
        }

        public Result<Statvfs> GetFileSystemStatus(string path)
        {
            var entry = GetFileSystemEntry(path);
            if (!entry.IsSuccessful())
            {
                return Result.Fail(entry.ErrorType.Value);
            }
            var buffer = new Statvfs
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
            };
            return Result.Ok(buffer);
        }

        private Result<IFileSystemEntry> GetFileSystemEntry(string path)
        {
            var parentDirectoryCheck = IsDirectoryValid(GetParentDirectory(path));
            if (!parentDirectoryCheck.IsSuccessful())
            {
                return parentDirectoryCheck;
            }

            var file = ReadFile(path, 0);
            if (file.IsSuccessful())
            {
                return Result.Ok(file.Value as IFileSystemEntry);
            }
            var directory = ReadDirectory(path);
            if (directory.IsSuccessful())
            {
                return Result.Ok(directory.Value as IFileSystemEntry);
            }
            return Result.Fail(directory.ErrorType.Value);
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
            return directoryRepository.IsDirectoriesExists(directory)
                ? Result.Ok()
                : Result.Fail(fileRepository.IsFileExists(directory) ? FileSystemError.NotDirectory : FileSystemError.NoEntry);
        }
    }
}