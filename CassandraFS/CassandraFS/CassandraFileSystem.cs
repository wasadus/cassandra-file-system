using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mono.Fuse.NETStandard;
using Mono.Unix.Native;

using Vostok.Logging.Abstractions;
using Vostok.Logging.File;

namespace CassandraFS
{
    public class CassandraFileSystem : FileSystem
    {
        private readonly ILog logger;
        private readonly FileSystemRepository fileSystemRepository;

        public CassandraFileSystem(string[] args, FileSystemRepository fileSystemRepository, ILog logger)
        {
            logger.Info("Parsing filesystem args...");
            var unhandled = ParseFuseArguments(args);
            if (unhandled.Length == 0)
            {
                throw new ArgumentException("Missing mountPoint");
            }

            MountPoint = unhandled[0];
            logger.Info("Parsing filesystem args... complete");
            this.logger = logger;
            this.fileSystemRepository = fileSystemRepository;
            FileLog.FlushAll();
        }

        protected override Errno OnGetPathStatus(string path, out Stat buf)
        {
            // Syscall.lstat
            logger.Info($"OnGetPathStatus({path})...");
            try
            {
                var stat = fileSystemRepository.GetPathStatus(path);
                buf = stat.Value;
                logger.Info($"OnGetPathStatus({path}, {buf}) -> {stat.ErrorType}, {buf.st_mode}, {buf.st_uid}");
                return ToErrno(stat.ErrorType);
            }
            catch (Exception e)
            {
                buf = new Stat();
                logger.Error($"OnGetPathStatus({path}, {buf}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnAccessPath(string path, AccessModes mask)
        {
            logger.Info($"OnAccessPath({path}, {mask})...");
            try
            {
                var error = fileSystemRepository.GetAccessToPath(path, mask);
                logger.Info($"OnAccessPath({path}, {mask}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnAccessPath({path}, {mask}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnReadSymbolicLink(string path, out string target)
        {
            logger.Info($"OnReadSymbolicLink({path})...");
            var error = Errno.ENOSYS;
            target = null;
            logger.Info($"OnReadSymbolicLink({path}) -> {error}");
            return error;
        }

        protected override Errno OnReadDirectory(string path, OpenedPathInfo fi, out IEnumerable<DirectoryEntry> paths)
        {
            logger.Info($"OnReadDirectory({path})...");
            try
            {
                var rawNames = fileSystemRepository.ReadDirectoryContent(path);
                rawNames.Add(new DirectoryEntry(".") {Stat = new Stat {st_mode = FilePermissions.S_IFDIR | FilePermissions.ACCESSPERMS}});
                rawNames.Add(new DirectoryEntry("..") {Stat = new Stat {st_mode = FilePermissions.S_IFDIR | FilePermissions.ACCESSPERMS}});
                paths = rawNames;
                logger.Info($"OnReadDirectory({path}) -> 0, {string.Join(";", paths.Select(x => x.Name))}");
                return 0;
            }
            catch (Exception e)
            {
                paths = null;
                logger.Error($"OnReadDirectory({path}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnCreateSpecialFile(string path, FilePermissions mode, ulong rdev)
        {
            logger.Info($"OnCreateSpecialFile({path}, {mode}, {rdev})...");
            try
            {
                mode |= FilePermissions.S_IFREG;
                var error = fileSystemRepository.CreateFile(path, mode, rdev);
                logger.Info($"OnCreateSpecialFile({path}, {mode}, {rdev}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnCreateSpecialFile({path}, {mode}, {rdev}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnCreateDirectory(string path, FilePermissions mode)
        {
            logger.Info($"OnCreateDirectory({path}, {mode})...");
            try
            {
                var error = fileSystemRepository.WriteDirectory(path, mode);
                logger.Info($"OnCreateDirectory({path}, {mode}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnCreateDirectory({path}, {mode}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnRemoveFile(string path)
        {
            logger.Info($"OnRemoveFile({path})...");
            try
            {
                var error = fileSystemRepository.DeleteFile(path);
                logger.Info($"OnRemoveFile({path}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnRemoveFile({path}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnRemoveDirectory(string path)
        {
            logger.Info($"OnRemoveDirectory({path})...");
            try
            {
                var error = fileSystemRepository.DeleteDirectory(path);
                logger.Info($"OnRemoveDirectory({path}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnRemoveDirectory({path}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnCreateSymbolicLink(string from, string to)
        {
            logger.Info($"OnCreateSymbolicLink({from}, {to})...");
            var error = Errno.EPERM;
            logger.Info($"OnCreateSymbolicLink({from}, {to}) -> {error}");
            return error;
        }

        protected override Errno OnRenamePath(string from, string to)
        {
            logger.Info($"OnRenamePath()...");
            if (from == null || to == null || from.Equals("") || to.Equals(""))
            {
                logger.Info($"OnRenamePath({from}, {to}) -> {Errno.ENOENT}");
                return Errno.ENOENT;
            }

            try
            {
                var result = fileSystemRepository.RenameFile(from, to);
                if (result.IsSuccessful())
                {
                    logger.Info($"OnRenamePath({from}, {to}) -> 0");
                    return 0;
                }

                result = fileSystemRepository.RenameDirectory(from, to);
                if (result.IsSuccessful())
                {
                    logger.Info($"OnRenamePath({from}, {to}) -> 0");
                    return 0;
                }

                logger.Info($"OnRenamePath({from}, {to}) -> {result.ErrorType}");
                return ToErrno(result.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnRenamePath({from}, {to}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnCreateHardLink(string from, string to)
        {
            logger.Info($"OnCreateHardLink({from}, {to})...");
            var error = Errno.EPERM;
            logger.Info($"OnCreateHardLink({from}, {to}) -> {error}");
            return error;
        }

        protected override Errno OnChangePathPermissions(string path, FilePermissions mode)
        {
            logger.Info($"OnChangePathPermissions({path}, {mode})...");
            try
            {
                var error = fileSystemRepository.ChangePathPermissions(path, mode);
                logger.Info($"OnChangePathPermissions({path}, {mode}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnChangePathPermissions({path}, {mode}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnChangePathOwner(string path, long uid, long gid)
        {
            logger.Info($"OnChangePathOwner({path}, {uid}, {gid})...");
            try
            {
                var error = fileSystemRepository.ChangePathOwner(path, (uint)uid, (uint)gid);
                logger.Info($"OnChangePathOwner({path}, {uid}, {gid}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnChangePathOwner({path}, {uid}, {gid}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnTruncateFile(string path, long size)
        {
            logger.Info($"OnTruncateFile()...");
            try
            {
                var file = fileSystemRepository.ReadFile(path, 0);
                if (!file.IsSuccessful())
                {
                    logger.Info($"OnTruncateFile({path}, {size}) -> {file.ErrorType}");
                    return ToErrno(file.ErrorType);
                }

                var truncatedData = file.Value.Data;
                Array.Resize(ref truncatedData, (int)size);
                file.Value.Data = truncatedData;
                fileSystemRepository.WriteFile(file.Value);
                logger.Info($"OnTruncateFile({path}, {size}) -> {file.ErrorType}");
                return 0;
            }
            catch (Exception e)
            {
                logger.Error($"OnTruncateFile({path}, {size}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnChangePathTimes(string path, ref Utimbuf buf)
        {
            //Syscall.utime
            logger.Info($"OnChangePathTimes({path}, {buf})...");
            var error = OnGetPathStatus(path, out _);
            logger.Info($"OnChangePathTimes({path}, {buf}) -> {error}");
            return error;
        }

        protected override Errno OnOpenHandle(string path, OpenedPathInfo info)
        {
            logger.Info($"OnOpenHandle({path}, {info.OpenFlags})...");
            try
            {
                var file = fileSystemRepository.ReadFile(path, info.OpenFlags);
                logger.Info($"OnOpenHandle({path}, {info.OpenFlags}) -> {file.ErrorType}");
                return ToErrno(file.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnOpenHandle({path}, {info.OpenFlags}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnReadHandle(
            string path,
            OpenedPathInfo info,
            byte[] buf,
            long offset,
            out int bytesRead)
        {
            logger.Info($"OnReadHandle({path}, {info.OpenFlags}, {info.OpenAccess}, {offset})...");
            bytesRead = 0;
            try
            {
                var file = fileSystemRepository.ReadFile(path, info.OpenFlags);
                if (!file.IsSuccessful())
                {
                    logger.Info($"OnReadHandle({path}, {info.OpenFlags}, {info.OpenAccess}, {offset}) -> {file.ErrorType}");
                    return ToErrno(file.ErrorType);
                }

                using var data = file.Value.Data.Length > 0 ? new MemoryStream(file.Value.Data) : new MemoryStream();
                data.Seek(offset, SeekOrigin.Begin);
                bytesRead = data.Read(buf, 0, buf.Length);
                logger.Info(
                    $"OnReadHandle({path}, {info.OpenFlags}, {info.OpenAccess}, {offset}) -> {bytesRead}, {file.ErrorType}");
                return 0;
            }
            catch (Exception e)
            {
                logger.Error(
                    $"OnReadHandle({path}, {info.OpenFlags}, {info.OpenAccess}, {offset}) -> {bytesRead}, error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnWriteHandle(
            string path,
            OpenedPathInfo info,
            byte[] buf,
            long offset,
            out int bytesWritten)
        {
            logger.Info($"OnWriteHandle({path}, {info.OpenAccess}, {info.OpenFlags}, {offset})...");
            bytesWritten = 0;
            try
            {
                var file = fileSystemRepository.ReadFile(path, info.OpenFlags);
                if (!file.IsSuccessful())
                {
                    logger.Error($"OnWriteHandle({path}, {info.OpenAccess}, {info.OpenFlags}, {offset}) -> {file.ErrorType}");
                    return ToErrno(file.ErrorType);
                }

                using var dataStream = new MemoryStream();
                if (file.Value.Data.Length > 0)
                {
                    dataStream.Write(file.Value.Data, 0, file.Value.Data.Length);
                }

                dataStream.Seek(offset, SeekOrigin.Begin);
                dataStream.Write(buf, 0, buf.Length);
                bytesWritten = buf.Length;
                file.Value.Data = dataStream.ToArray();
                fileSystemRepository.WriteFile(file.Value);
                logger.Info(
                    $"OnWriteHandle({path}, {info.OpenAccess}, {info.OpenFlags}, {offset}) -> {bytesWritten}, {file.ErrorType}");
                return 0;
            }
            catch (Exception e)
            {
                logger.Error(
                    $"OnWriteHandle({path}, {info.OpenAccess}, {info.OpenFlags}, {offset}) -> {bytesWritten}, error: {e.Message}, {e.StackTrace}"
                );
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnGetFileSystemStatus(string path, out Statvfs stbuf)
        {
            logger.Info($"OnGetFileSystemStatus()");
            try
            {
                var result = fileSystemRepository.GetFileSystemStatus(path);
                stbuf = result.Value;
                logger.Info($"OnGetFileSystemStatus({path}, {stbuf}) -> {result.ErrorType}");
                return ToErrno(result.ErrorType);
            }
            catch (Exception e)
            {
                stbuf = new Statvfs();
                logger.Error($"OnGetFileSystemStatus({path}, {stbuf}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnReleaseHandle(string path, OpenedPathInfo info)
        {
            logger.Info($"OnReleaseHandle({path}, {info}) -> 0");
            return 0;
        }

        protected override Errno OnSynchronizeHandle(string path, OpenedPathInfo info, bool onlyUserData)
        {
            logger.Info($"OnSynchronizeHandle({path}, {info}, {onlyUserData}) -> 0");
            return 0;
        }

        protected override Errno OnSetPathExtendedAttribute(string path, string name, byte[] value, XattrFlags flags)
        {
            logger.Info($"OnSetPathExtendedAttribute({path}, {name}, {flags})...");
            try
            {
                var error = fileSystemRepository.SetExtendedAttribute(path, name, value, flags);
                logger.Info($"OnSetPathExtendedAttribute({path}, {name}, {flags}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error(
                    $"OnSetPathExtendedAttribute({path}, {name}, {flags}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnGetPathExtendedAttribute(
            string path,
            string name,
            byte[] value,
            out int bytesWritten)
        {
            logger.Info($"OnGetPathExtendedAttribute({path}, {name})...");
            try
            {
                var result = fileSystemRepository.GetExtendedAttribute(path, name, value);
                bytesWritten = result.Value;
                logger.Info($"OnGetPathExtendedAttribute({path}, {name}) -> {result.ErrorType}");
                return ToErrno(result.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error(
                    $"OnGetPathExtendedAttribute({path}, {name}) -> {value}, error: {e.Message}, {e.StackTrace}");
                bytesWritten = 0;
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnListPathExtendedAttributes(string path, out string[] names)
        {
            logger.Info($"OnListPathExtendedAttributes({path})...");
            try
            {
                var result = fileSystemRepository.GetExtendedAttributesList(path);
                names = result.Value;
                logger.Info($"OnListPathExtendedAttributes({path}) -> {result.ErrorType}");
                return ToErrno(result.ErrorType);
            }
            catch (Exception e)
            {
                names = new string[0];
                logger.Error($"OnListPathExtendedAttributes({path}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnRemovePathExtendedAttribute(string path, string name)
        {
            logger.Info($"OnRemovePathExtendedAttribute({path}, {name})...");
            try
            {
                var error = fileSystemRepository.RemoveExtendedAttribute(path, name);
                logger.Info($"OnRemovePathExtendedAttribute({path}, {name}) -> {error.ErrorType}");
                return ToErrno(error.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"OnRemovePathExtendedAttribute({path}, {name}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        protected override Errno OnLockHandle(string path, OpenedPathInfo info, FcntlCommand cmd, ref Flock @lock)
        {
            logger.Info($"OnLockHandle({path}, {info}, {cmd}, {@lock.l_type})...");
            var error = OnOpenHandle(path, info);
            logger.Info($"OnLockHandle({path}, {info}, {cmd}, {@lock.l_type}) -> {error}");
            return error;
        }

        private static Errno ToErrno(FileSystemError? error) => error switch
        {
            FileSystemError.IsDirectory => Errno.EISDIR,
            FileSystemError.NotDirectory => Errno.ENOTDIR,
            FileSystemError.NoEntry => Errno.ENOENT,
            FileSystemError.AlreadyExist => Errno.EEXIST,
            FileSystemError.DirectoryNotEmpty => Errno.ENOTEMPTY,
            FileSystemError.InvalidArgument => Errno.EINVAL,
            FileSystemError.NoAttribute => Errno.ENOATTR,
            FileSystemError.OutOfRange => Errno.ERANGE,
            FileSystemError.PermissionDenied => Errno.EPERM,
            FileSystemError.AccessDenied => Errno.EACCES,
            null => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
        };
    }
}