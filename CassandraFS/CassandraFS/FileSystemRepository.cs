using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CassandraFS.CassandraHandler;
using CassandraFS.Models;
using Mono.Fuse.NETStandard;
using Mono.Unix.Native;

using Vostok.Logging.Abstractions;

namespace CassandraFS
{
    public class FileSystemRepository
    {
        private static readonly HashSet<string> rootDirPaths = new HashSet<string> {"", "."};
        private readonly FileRepository fileRepository;
        private readonly DirectoryRepository directoryRepository;
        private readonly ILog logger;

        public FileSystemRepository(FileRepository fileRepository, DirectoryRepository directoryRepository, ILog logger)
        {
            this.directoryRepository = directoryRepository;
            this.fileRepository = fileRepository;
            this.logger = logger.ForContext("FileSystemRepository");
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

        public Errno TryReadDirectory(string path, out DirectoryModel directory)
        {
            directory = null;
            if (!IsDirectoryValid(GetParentDirectory(path), out var error))
            {
                logger.Info($"TryReadDirectory -> !IsDirectoryValid {GetParentDirectory(path)}");
                return error;
            }

            if (fileRepository.IsFileExists(path))
            {
                return Errno.ENOTDIR;
            }

            directory = directoryRepository.ReadDirectory(path);
            logger.Info($"TryReadDirectory -> {directory?.Name}");
            return directory == null ? Errno.ENOENT : 0;
        }

        public Errno TryWriteDirectory(string path, FilePermissions mode)
        {
            var dirName = GetFileName(path);
            var parentDirPath = GetParentDirectory(path);

            if (!IsDirectoryValid(parentDirPath, out var error))
            {
                return error;
            }

            if (directoryRepository.IsDirectoryExists(path))
            {
                return Errno.EEXIST;
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
            return 0;
        }

        public Errno TryDeleteDirectory(string path)
        {
            if (!IsDirectoryValid(path, out var error))
            {
                return error;
            }

            if (!IsDirectoryEmpty(path))
            {
                return Errno.ENOTEMPTY;
            }

            directoryRepository.DeleteDirectory(path);
            return 0;
        }

        public Errno TryRenameDirectory(string from, string to)
        {
            var error = TryReadDirectory(from, out var directory);

            if (error != 0)
            {
                return error;
            }

            if (to.StartsWith(from))
            {
                return Errno.EINVAL;
            }

            if (directoryRepository.IsDirectoryExists(to))
            {
                return Errno.EEXIST;
            }

            // TODO надо файлы тоже перенести
            var parentDirPath = GetParentDirectory(to);
            var dirName = GetFileName(to);
            directoryRepository.WriteDirectory(new DirectoryModel
                {
                    Path = parentDirPath,
                    Name = dirName,
                    FilePermissions = directory.FilePermissions,
                    UID = directory.UID,
                    GID = directory.GID,
                    ModifiedTimestamp = DateTimeOffset.Now
                });
            directoryRepository.DeleteDirectory(from);
            return 0;
        }

        public Errno TryReadFile(string path, OpenFlags flags, out FileModel file)
        {
            // O_APPEND not implemented
            var fileName = GetFileName(path);
            var parentDirPath = GetParentDirectory(path);
            file = null;
            if (!IsDirectoryValid(parentDirPath, out var error))
            {
                return error;
            }

            if (directoryRepository.IsDirectoryExists(path))
            {
                return Errno.EISDIR;
            }

            file = fileRepository.ReadFile(path);
            if ((flags & OpenFlags.O_CREAT) == 0 && file == null)
            {
                return Errno.ENOENT;
            }

            if (file != null)
            {
                if ((flags & OpenFlags.O_TRUNC) != 0)
                {
                    file.Data = new byte[0];
                }
                return 0;
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
            return 0;
        }

        public Errno TryWriteFile(FileModel file)
        {
            fileRepository.WriteFile(file);
            return 0;
        }

        public Errno TryCreateFile(string path, FilePermissions mode, ulong rdev)
        {
            var fileName = GetFileName(path);
            var parentDirPath = GetParentDirectory(path);

            if (!IsDirectoryValid(parentDirPath, out var error))
            {
                return error;
            }

            if (fileRepository.IsFileExists(path))
            {
                return Errno.EEXIST;
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
            return 0;
        }

        public Errno TryDeleteFile(string path)
        {
            if (!IsDirectoryValid(GetParentDirectory(path), out var error))
            {
                return error;
            }

            if (!fileRepository.IsFileExists(path))
            {
                return Errno.ENOENT;
            }

            fileRepository.DeleteFile(path);
            return 0;
        }

        public Errno TryRenameFile(string from, string to)
        {
            var error = TryReadFile(from, 0, out var file);
            if (error != 0)
            {
                return error;
            }

            if (directoryRepository.IsDirectoryExists(to))
            {
                return Errno.EISDIR;
            }

            if (fileRepository.IsFileExists(to))
            {
                return Errno.EEXIST;
            }

            fileRepository.DeleteFile(from);
            var parentDirPath = GetParentDirectory(to);
            var fileName = GetFileName(to);
            file.Name = fileName;
            file.Path = parentDirPath;
            file.ModifiedTimestamp = DateTimeOffset.Now;
            fileRepository.WriteFile(file);
            return 0;
        }

        public Errno TrySetExtendedAttribute(string path, string name, byte[] value, XattrFlags flags)
        {
            var error = TryReadFile(path, 0, out var file);
            if (error != 0)
            {
                return error;
            }

            var attributes = file.ExtendedAttributes.Attributes;
            if (attributes.ContainsKey(name) && flags == XattrFlags.XATTR_CREATE)
            {
                return Errno.EEXIST;
            }

            if (!attributes.ContainsKey(name) && flags == XattrFlags.XATTR_REPLACE)
            {
                return Errno.ENOATTR;
            }

            attributes.Add(name, value);
            return TryWriteFile(file);
        }

        public Errno TryGetExtendedAttribute(string path, string name, byte[] value, out int bytesWritten)
        {
            bytesWritten = 0;
            var error = TryReadFile(path, 0, out var file);
            if (error != 0)
            {
                return error;
            }

            var attributes = file.ExtendedAttributes.Attributes;
            if (!attributes.ContainsKey(name))
            {
                return Errno.ENOATTR;
            }

            var attribute = attributes[name];
            if (attribute.Length > value.Length)
            {
                return Errno.ERANGE;
            }

            using var ms = new MemoryStream(value);
            ms.Write(attribute);
            bytesWritten = attribute.Length;
            return 0;
        }

        public Errno TryGetExtendedAttributesList(string path, out string[] names)
        {
            names = null;
            var error = TryReadFile(path, 0, out var file);
            if (error != 0)
            {
                return error;
            }
            names = file.ExtendedAttributes.Attributes.Keys.ToArray();
            return 0;
        }

        public Errno TryRemoveExtendedAttribute(string path, string name)
        {
            var error = TryReadFile(path, 0, out var file);
            if (error != 0)
            {
                return error;
            }

            var attributes = file.ExtendedAttributes.Attributes;
            if (!attributes.ContainsKey(name))
            {
                return Errno.ENOATTR;
            }

            attributes.Remove(name);
            return TryWriteFile(file);
        }

        public Errno TryGetPathStatus(string path, out Stat buffer)
        {
            buffer = new Stat();
            var error = TryGetFileSystemEntry(path, out var entry);
            if (error != 0)
            {
                return error;
            }
            buffer = entry.GetStat();
            logger.Info($"TryGetPathStatus -> buffer {buffer.st_mode}, {buffer.st_gid}, {buffer.st_uid}, {buffer.st_atim}");
            return 0;
        }

        public Errno TryChangePathPermissions(string path, FilePermissions permissions)
        {
            var error = TryGetFileSystemEntry(path, out var entry);
            if (error != 0)
            {
                return error;
            }

            var euid = Syscall.geteuid();
            if (euid != 0 && entry.UID != euid)
            {
                return Errno.EPERM;
            }

            WriteFileSystemEntry(entry);
            return 0;
        }

        public Errno TryChangePathOwner(string path, uint newUID, uint newGID)
        {
            var error = TryGetFileSystemEntry(path, out var entry);
            if (error != 0)
            {
                return error;
            }

            if (!entry.IsChownPermissionsOk(newUID, newGID))
            {
                return Errno.EPERM;
            }
            entry.UID = newUID;
            entry.GID = newGID;

            WriteFileSystemEntry(entry);
            return 0;
        }

        public Errno TryGetAccessToPath(string path, AccessModes mode)
        {
            var error = TryGetFileSystemEntry(path, out var entry);
            if (error != 0)
            {
                return error;
            }

            var permissions = entry.FilePermissions;
            var fileUID = entry.UID;
            var fileGID = entry.GID;

            var userUID = Syscall.getuid();
            var userGID = Syscall.getgid();
            return ((AccessModes.R_OK & mode) != 0 && !permissions.CanUserRead(userUID, userGID, fileUID, fileGID))
                   || ((AccessModes.W_OK & mode) != 0 && !permissions.CanUserWrite(userUID, userGID, fileUID, fileGID))
                   || ((AccessModes.X_OK & mode) != 0 && !permissions.CanUserExecute(userUID, userGID, fileUID, fileGID))
                       ? Errno.EACCES
                       : 0;
        }

        public Errno TryGetFileSystemStatus(string path, out Statvfs buffer)
        {
            buffer = new Statvfs();
            var error = TryGetFileSystemEntry(path, out var _);
            if (error != 0)
            {
                return error;
            }

            buffer.f_bsize = 4096;
            buffer.f_frsize = 4096;
            buffer.f_blocks = 1; // just not zero
            buffer.f_bfree = ulong.MaxValue;
            buffer.f_bavail = ulong.MaxValue;
            buffer.f_files = 4096; // Maybe it should be valid counter
            buffer.f_ffree = ulong.MaxValue;
            buffer.f_favail = ulong.MaxValue;
            buffer.f_fsid = 1;
            buffer.f_namemax = 255;

            return 0;
        }

        // TODO Может сделать bool и out Errno?
        private Errno TryGetFileSystemEntry(string path, out IFileSystemEntry entry)
        {
            entry = null;
            if (!IsDirectoryValid(GetParentDirectory(path), out var error))
            {
                logger.Info($"TryGetFileSystemEntry -> !IsDirectoryValid {GetParentDirectory(path)}");
                return error;
            }

            error = TryReadFile(path, 0, out var file);
            if (error == 0)
            {
                logger.Info($"TryGetFileSystemEntry -> file {file.Name}");
                entry = file;
                return 0;
            }
            error = TryReadDirectory(path, out var dir);
            if (error == 0)
            {
                logger.Info($"TryGetFileSystemEntry -> directory {dir.Name}");
                entry = dir;
                return 0;
            }
            logger.Info($"TryGetFileSystemEntry -> error {error}");
            return error;
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

        private bool IsDirectoryValid(string directory, out Errno error)
        {
            error = 0;
            if (!directoryRepository.IsDirectoryExists(directory))
            {
                error = fileRepository.IsFileExists(directory) ? Errno.ENOTDIR : Errno.ENOENT;
                return false;
            }
            return true;
        }
    }
}