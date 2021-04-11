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
            var (error, buffer) = WithLogging(
                () => fileSystemRepository.GetPathStatus(path),
                $"OnGetPathStatus({path})",
                result => $"{result.Value.st_mode}, {result.Value.st_uid}");
            buf = buffer;
            return error;
        }

        protected override Errno OnAccessPath(string path, AccessModes mask)
        {
            return WithLogging(
                () => fileSystemRepository.GetAccessToPath(path, mask),
                $"OnAccessPath({path}, {mask})");
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
            var (error, resultPaths) = WithLogging(
                () =>
                    {
                        var rawNames = fileSystemRepository.ReadDirectoryContent(path);
                        rawNames.Add(new DirectoryEntry(".") {Stat = new Stat {st_mode = FilePermissions.S_IFDIR | FilePermissions.ACCESSPERMS}});
                        rawNames.Add(new DirectoryEntry("..") {Stat = new Stat {st_mode = FilePermissions.S_IFDIR | FilePermissions.ACCESSPERMS}});
                        return Result.Ok(rawNames);
                    },
                $"OnReadDirectory({path})",
                result => $"{string.Join(";", result.Value.Select(x => x.Name))}");
            paths = resultPaths;
            return error;
        }

        protected override Errno OnCreateSpecialFile(string path, FilePermissions mode, ulong rdev)
        {
            return WithLogging(
                () => fileSystemRepository.CreateFile(path, mode | FilePermissions.S_IFREG, rdev),
                $"OnCreateSpecialFile({path}, {mode}, {rdev})");
        }

        protected override Errno OnCreateDirectory(string path, FilePermissions mode)
        {
            return WithLogging(
                () => fileSystemRepository.WriteDirectory(path, mode),
                $"OnCreateDirectory({path}, {mode})");
        }

        protected override Errno OnRemoveFile(string path)
        {
            return WithLogging(
                () => fileSystemRepository.DeleteFile(path),
                $"OnRemoveFile({path})");
        }

        protected override Errno OnRemoveDirectory(string path)
        {
            return WithLogging(
                () => fileSystemRepository.DeleteDirectory(path),
                $"OnRemoveDirectory({path})");
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

            return WithLogging(
                () =>
                    {
                        var fileResult = fileSystemRepository.RenameFile(from, to);
                        return fileResult.IsSuccessful() ? fileResult : fileSystemRepository.RenameDirectory(from, to);
                    },
                $"OnRenamePath({from}, {to})");
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
            return WithLogging(
                () => fileSystemRepository.ChangePathPermissions(path, mode),
                $"OnChangePathPermissions({path}, {mode})");
        }

        protected override Errno OnChangePathOwner(string path, long uid, long gid)
        {
            return WithLogging(
                () => fileSystemRepository.ChangePathOwner(path, (uint)uid, (uint)gid),
                $"OnChangePathOwner({path}, {uid}, {gid})");
        }

        protected override Errno OnTruncateFile(string path, long size)
        {
            return WithLogging(
                () => fileSystemRepository.ReadFile(path).Then(file =>
                    {
                        var truncatedData = file.Data;
                        Array.Resize(ref truncatedData, (int)size);
                        file.Data = truncatedData;
                        fileSystemRepository.WriteFile(file);
                        return Result.Ok();
                    }),
                $"OnTruncateFile({path}, {size})");
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
            return WithLogging(
                () => fileSystemRepository.ReadFile(path, info.OpenFlags),
                $"OnOpenHandle({path}, {info.OpenFlags})");
        }

        protected override Errno OnReadHandle(
            string path,
            OpenedPathInfo info,
            byte[] buf,
            long offset,
            out int bytesRead)
        {
            var (error, result) = WithLogging(
                () => fileSystemRepository.ReadFile(path, info.OpenFlags).Then(file =>
                    {
                        using var data = file.Data.Length > 0 ? new MemoryStream(file.Data) : new MemoryStream();
                        data.Seek(offset, SeekOrigin.Begin);
                        var read = data.Read(buf, 0, buf.Length);
                        return Result.Ok(read);
                    }),
                $"OnReadHandle({path}, {info.OpenFlags}, {info.OpenAccess}, {offset})",
                res => $"{res.Value}");
            bytesRead = result;
            return error;
        }

        protected override Errno OnWriteHandle(
            string path,
            OpenedPathInfo info,
            byte[] buf,
            long offset,
            out int bytesWritten)
        {
            var (error, result) = WithLogging(
                () => fileSystemRepository.ReadFile(path, info.OpenFlags).Then(file =>
                    {
                        using var dataStream = new MemoryStream();
                        if (file.Data.Length > 0)
                        {
                            dataStream.Write(file.Data, 0, file.Data.Length);
                        }

                        dataStream.Seek(offset, SeekOrigin.Begin);
                        dataStream.Write(buf, 0, buf.Length);
                        var written = buf.Length;
                        file.Data = dataStream.ToArray();
                        fileSystemRepository.WriteFile(file);
                        return Result.Ok(written);
                    }),
                $"OnWriteHandle({path}, {info.OpenAccess}, {info.OpenFlags}, {offset})",
                res => $"{res.Value}");
            bytesWritten = result;
            return error;
        }

        protected override Errno OnGetFileSystemStatus(string path, out Statvfs stbuf)
        {
            var (error, buffer) = WithLogging(
                () => fileSystemRepository.GetFileSystemStatus(path),
                $"OnGetFileSystemStatus({path})",
                _ => "");
            stbuf = buffer;
            return error;
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
            return WithLogging(
                () => fileSystemRepository.SetExtendedAttribute(path, name, value, flags),
                $"OnSetPathExtendedAttribute({path}, {name}, {flags})");
        }

        protected override Errno OnGetPathExtendedAttribute(
            string path,
            string name,
            byte[] value,
            out int bytesWritten)
        {
            var (error, written) = WithLogging(
                () => fileSystemRepository.GetExtendedAttribute(path, name, value),
                $"OnGetPathExtendedAttribute({path}, {name})",
                res => $"{res.Value}");
            bytesWritten = written;
            return error;
        }

        protected override Errno OnListPathExtendedAttributes(string path, out string[] names)
        {
            var (error, attributes) = WithLogging(
                () => fileSystemRepository.GetExtendedAttributesList(path),
                $"OnListPathExtendedAttributes({path})",
                res => $"{res.Value}");
            names = attributes;
            return error;
        }

        protected override Errno OnRemovePathExtendedAttribute(string path, string name)
        {
            return WithLogging(
                () => fileSystemRepository.RemoveExtendedAttribute(path, name),
                $"OnRemovePathExtendedAttribute({path}, {name})");
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

        private (Errno, T) WithLogging<T>(Func<Result<T>> func, string context, Func<Result<T>, string> logResult)
        {
            logger.Info($"{context} starts");
            try
            {
                var result = func();
                logger.Info($"{context} -> {result.ErrorType}; {logResult(result)}");
                return (ToErrno(result.ErrorType), result.Value);
            }
            catch (Exception e)
            {
                logger.Error($"{context} -> error: {e.Message}, {e.StackTrace}");
                return (Errno.ENOSYS, default);
            }
        }

        private Errno WithLogging(Func<Result> func, string context)
        {
            logger.Info($"{context} starts");
            try
            {
                var result = func();
                logger.Info($"{context} -> {result.ErrorType}");
                return ToErrno(result.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"{context} -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }

        private Errno WithLogging<T>(Func<Result<T>> func, string context)
        {
            logger.Info($"{context} starts");
            try
            {
                var result = func();
                logger.Info($"{context} -> {result.ErrorType}");
                return ToErrno(result.ErrorType);
            }
            catch (Exception e)
            {
                logger.Error($"{context} -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOSYS;
            }
        }
    }
}