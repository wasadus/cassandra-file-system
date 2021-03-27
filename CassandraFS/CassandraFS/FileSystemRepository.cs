using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public Errno TryReadDirectory(string path, out DirectoryModel directory)
        {
            var parentDirPath = GetParentDirectory(path);
            directory = null;
            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
            }

            if (fileRepository.IsFileExists(path))
            {
                return Errno.ENOTDIR;
            }

            directory = directoryRepository.ReadDirectory(path);
            return directory == null ? Errno.ENOENT : 0;
        }

        public Errno TryWriteDirectory(string path, FilePermissions mode)
        {
            var dirName = GetFileName(path);
            var parentDirPath = GetParentDirectory(path);

            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
            }

            if (directoryRepository.IsDirectoryExists(path))
            {
                return Errno.EEXIST;
            }

            var uid = Syscall.getuid();
            var gid = Syscall.getgid();
            directoryRepository.WriteDirectory(new DirectoryModel
            {
                Path = parentDirPath, Name = dirName, FilePermissions = mode, UID = uid, GID = gid, ModifiedTimestamp = DateTimeOffset.Now
            });
            return 0;
        }

        public Errno TryDeleteDirectory(string path)
        {
            if (!directoryRepository.IsDirectoryExists(path))
            {
                return fileRepository.IsFileExists(path) ? Errno.ENOTDIR : Errno.ENOENT;
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

            // TODO Возможно надо файлы тоже перенести?
            var parentDirPath = GetParentDirectory(to);
            var dirName = GetFileName(to);
            directoryRepository.WriteDirectory(new DirectoryModel { Path = parentDirPath, Name = dirName, FilePermissions = directory.FilePermissions, UID = directory.UID, GID = directory.GID, ModifiedTimestamp = DateTimeOffset.Now });
            directoryRepository.DeleteDirectory(from);
            return 0;
        }

        public Errno TryReadFile(string path, OpenFlags flags, out FileModel file)
        {
            // O_APPEND not implemented
            var fileName = GetFileName(path);
            var parentDirPath = GetParentDirectory(path);
            file = null;
            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
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
                    Path = parentDirPath, Name = fileName, Data = new byte[0], ModifiedTimestamp = now,
                    ExtendedAttributes = new ExtendedAttributes(),
                    FilePermissions = FilePermissions.ACCESSPERMS | FilePermissions.S_IFREG,
                    GID = egid, UID = euid, ContentGUID = Guid.Empty
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

            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
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
                    Path = parentDirPath, Name = fileName, Data = new byte[0],
                    ExtendedAttributes = new ExtendedAttributes(), ModifiedTimestamp = now,
                    FilePermissions = mode, GID = gid, UID = uid, ContentGUID = Guid.Empty
                };
            fileRepository.WriteFile(file);
            return 0;
        }

        public Errno TryDeleteFile(string path)
        {
            var parentDirPath = GetParentDirectory(path);
            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
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
            var parentDirPath = GetParentDirectory(path);
            buffer = new Stat();
            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
            }

            var error = TryGetFileSystemEntry(path, out var entry);
            if (error != 0)
            {
                return error;
            }
            buffer = entry.GetStat();
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

            if (!IsChownPermissionsOk(entry, newUID, newGID))
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
            return ((AccessModes.R_OK & mode) != 0 && !CanUserRead(permissions, userUID, userGID, fileUID, fileGID))
                || ((AccessModes.W_OK & mode) != 0 && !CanUserWrite(permissions, userUID, userGID, fileUID, fileGID))
                || ((AccessModes.X_OK & mode) != 0 && !CanUserExecute(permissions, userUID, userGID, fileUID, fileGID))
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

        private Errno TryGetFileSystemEntry(string path, out IFileSystemEntry entry)
        {
            entry = null;
            var parentDirPath = GetParentDirectory(path);
            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
            }

            var error = TryReadFile(path, 0, out var file);
            if (error == 0)
            {
                entry = file;
                return 0;
            }
            error = TryReadDirectory(path, out var dir);
            if (error == 0)
            {
                entry = dir;
                return 0;
            }
            return error;
        }

        private bool CanUserRead(FilePermissions permissions, uint userUID, uint userGID, uint fileUID, uint fileGID) =>
            userUID == 0
                || (userUID == fileUID && (permissions & FilePermissions.S_IRUSR) != 0)
                || (userGID == fileGID && (permissions & FilePermissions.S_IRGRP) != 0)
                || (permissions & FilePermissions.S_IROTH) != 0;

        private bool CanUserWrite(FilePermissions permissions, uint userUID, uint userGID, uint fileUID, uint fileGID) =>
            userUID == 0
                || (userUID == fileUID && (permissions & FilePermissions.S_IWUSR) != 0)
                || (userGID == fileGID && (permissions & FilePermissions.S_IWGRP) != 0)
                || (permissions & FilePermissions.S_IWOTH) != 0;

        private bool CanUserExecute(FilePermissions permissions, uint userUID, uint userGID, uint fileUID, uint fileGID) =>
            userUID == 0
                || (userUID == fileUID && (permissions & FilePermissions.S_IXUSR) != 0)
                || (userGID == fileGID && (permissions & FilePermissions.S_IXGRP) != 0)
                || (permissions & FilePermissions.S_IXOTH) != 0;

        private bool IsChownPermissionsOk(IFileSystemEntry entry, uint newUID, uint newGID)
        {
            var userUID = Syscall.getuid();
            return userUID == 0 || (entry.UID == newUID && (entry.GID == newGID || userUID == entry.UID));
        }

        private void WriteFileSystemEntry(IFileSystemEntry entry)
        {
            switch (entry)
            {
                case FileModel _:
                    fileRepository.WriteFile(entry as FileModel);
                    return;
                case DirectoryModel _:
                    directoryRepository.WriteDirectory(entry as DirectoryModel);
                    return;
                default:
                    throw new NotImplementedException($"Unsupported FileSystemEntry type: {entry}");
            }
        }
    }
}