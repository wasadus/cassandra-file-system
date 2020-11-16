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

        public List<DirectoryEntry> ReadDirectoryContent(string path) =>
            directoryRepository.ReadDirectoryContent(path)
                               .Concat(fileRepository.ReadDirectoryContent(path))
                               .ToList();

        public bool IsDirectoryEmpty(string path) => !(fileRepository.IsFilesExists(path) || directoryRepository.IsDirectoriesExists(path));

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
            if (directory == null)
            {
                return Errno.ENOENT;
            }

            return 0;
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

            directoryRepository.WriteDirectory(new DirectoryModel {Path = parentDirPath, Name = dirName});
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
            directoryRepository.DeleteDirectory(from);
            var parentDirPath = GetParentDirectory(to);
            var dirName = GetFileName(to);
            directoryRepository.WriteDirectory(new DirectoryModel {Path = parentDirPath, Name = dirName});
            return 0;
        }

        public Errno TryReadFile(string path, OpenFlags flags, out FileModel file)
        {
            // TODO O_APPEND, O_TRUNC
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
                return 0;
            }

            var now = DateTimeOffset.Now;
            file = new FileModel
                {
                    Path = parentDirPath, Name = fileName, Data = new byte[0], ModifiedTimestamp = now,
                    ExtendedAttributes = new ExtendedAttributes()
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
            var file = new FileModel
                {
                    Path = parentDirPath, Name = fileName, Data = new byte[0],
                    ExtendedAttributes = new ExtendedAttributes(), ModifiedTimestamp = now
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
            buffer = new Stat {st_nlink = 1};
            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                buffer.st_nlink = 0;
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
            }

            if (!directoryRepository.IsDirectoryExists(path))
            {
                var error = TryReadFile(path, 0, out var file);
                if (error != 0)
                {
                    buffer.st_nlink = 0;
                    return error;
                }

                buffer.st_mode = FilePermissions.S_IFREG | FilePermissions.ACCESSPERMS;
                buffer.st_size = file.Data?.LongLength ?? 0;
                buffer.st_blocks = file.Data?.LongLength / 512 ?? 0;
                buffer.st_blksize = buffer.st_size; // Optimal size for buffer in I/O operations
                buffer.st_atim = DateTimeOffset.Now.ToTimespec(); // access
                buffer.st_mtim = file.ModifiedTimestamp.ToTimespec(); // modified
                return 0;
            }
            buffer.st_mode = FilePermissions.S_IFDIR | FilePermissions.ACCESSPERMS;
            return 0;
        }

        public Errno TryGetFileSystemStatus(string path, out Statvfs buffer)
        {
            var parentDirPath = GetParentDirectory(path);
            buffer = new Statvfs();
            if (!directoryRepository.IsDirectoryExists(parentDirPath))
            {
                return fileRepository.IsFileExists(parentDirPath) ? Errno.ENOTDIR : Errno.ENOENT;
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

            if (directoryRepository.IsDirectoryExists(path))
            {
                return 0;
            }

            var error = TryReadFile(path, 0, out var file);
            if (error == 0)
            {
                return 0;
            }

            buffer = new Statvfs();
            return error;
        }
    }
}