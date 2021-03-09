using System;
using System.Collections.Generic;
using System.IO;

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
                var error = fileSystemRepository.TryGetPathStatus(path, out buf);
                logger.Info($"OnGetPathStatus({path}, {buf}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                buf = new Stat();
                logger.Error($"OnGetPathStatus({path}, {buf}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
            }
        }

        protected override Errno OnAccessPath(string path, AccessModes mask)
        {
            logger.Info($"OnAccessPath({path}, {mask})...");
            var error = OnGetPathStatus(path, out _);
            logger.Info($"OnAccessPath({path}, {mask}) -> {error}");
            return error;
        }

        protected override Errno OnReadSymbolicLink(string path, out string target)
        {
            logger.Info($"OnReadSymbolicLink({path})...");
            var error = Errno.EACCES;
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
                rawNames.Add(new DirectoryEntry("."));
                rawNames.Add(new DirectoryEntry(".."));
                paths = rawNames;
                logger.Info($"OnReadDirectory({path}) -> 0, {string.Join(";", paths)}");
                return 0;
            }
            catch (Exception e)
            {
                paths = null;
                logger.Error($"OnReadDirectory({path}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
            }
        }

        protected override Errno OnCreateSpecialFile(string path, FilePermissions mode, ulong rdev)
        {
            logger.Info($"OnCreateSpecialFile({path}, {mode}, {rdev})...");
            try
            {
                var error = fileSystemRepository.TryCreateFile(path, mode, rdev);
                logger.Info($"OnCreateSpecialFile({path}, {mode}, {rdev}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error($"OnCreateSpecialFile({path}, {mode}, {rdev}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
            }
        }

        protected override Errno OnCreateDirectory(string path, FilePermissions mode)
        {
            logger.Info($"OnCreateDirectory({path}, {mode})...");
            try
            {
                var error = fileSystemRepository.TryWriteDirectory(path, mode);
                logger.Info($"OnCreateDirectory({path}, {mode}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error($"OnCreateDirectory({path}, {mode}) -> error: {e.Message}, {e.StackTrace}");
                return 0;
            }
        }

        protected override Errno OnRemoveFile(string path)
        {
            logger.Info($"OnRemoveFile({path})...");
            try
            {
                var error = fileSystemRepository.TryDeleteFile(path);
                logger.Info($"OnRemoveFile({path}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error($"OnRemoveFile({path}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
            }
        }

        protected override Errno OnRemoveDirectory(string path)
        {
            logger.Info($"OnRemoveDirectory({path})...");
            try
            {
                var error = fileSystemRepository.TryDeleteDirectory(path);
                logger.Info($"OnRemoveDirectory({path}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error($"OnRemoveDirectory({path}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
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
                var error = fileSystemRepository.TryRenameFile(from, to);
                if (error == 0)
                {
                    logger.Info($"OnRenamePath({from}, {to}) -> 0");
                    return 0;
                }

                error = fileSystemRepository.TryRenameDirectory(from, to);
                if (error == 0)
                {
                    logger.Info($"OnRenamePath({from}, {to}) -> 0");
                    return 0;
                }

                logger.Info($"OnRenamePath({from}, {to}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error($"OnRenamePath({from}, {to}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
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
            var error = OnGetPathStatus(path, out _);
            logger.Info($"OnChangePathPermissions({path}, {mode}) -> {error}");
            return error;
        }

        protected override Errno OnChangePathOwner(string path, long uid, long gid)
        {
            logger.Info($"OnChangePathOwner({path}, {uid}, {gid})...");
            var error = OnGetPathStatus(path, out _);
            logger.Info($"OnChangePathOwner({path}, {uid}, {gid}) -> {error}");
            return error;
        }

        protected override Errno OnTruncateFile(string path, long size)
        {
            logger.Info($"OnTruncateFile()...");
            try
            {
                var error = fileSystemRepository.TryReadFile(path, 0, out var file);
                if (error != 0)
                {
                    logger.Info($"OnTruncateFile({path}, {size}) -> {error}");
                    return error;
                }

                var truncatedData = file.Data;
                Array.Resize(ref truncatedData, (int)size);
                file.Data = truncatedData;
                error = fileSystemRepository.TryWriteFile(file);
                logger.Info($"OnTruncateFile({path}, {size}) -> {error}");
                return 0;
            }
            catch (Exception e)
            {
                logger.Error($"OnTruncateFile({path}, {size}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
            }
        }

        protected override Errno OnChangePathTimes(string path, ref Utimbuf buf)
        {
            //Syscall.utime; TODO Надо ли?
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
                var error = fileSystemRepository.TryReadFile(path, info.OpenFlags, out _);
                logger.Info($"OnOpenHandle({path}, {info.OpenFlags}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error($"OnOpenHandle({path}, {info.OpenFlags}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
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
                var error = fileSystemRepository.TryReadFile(path, info.OpenFlags, out var file);
                if (error != 0)
                {
                    logger.Info($"OnReadHandle({path}, {info.OpenFlags}, {info.OpenAccess}, {offset}) -> {error}");
                    return error;
                }

                using var data = file.Data.Length > 0 ? new MemoryStream(file.Data) : new MemoryStream();
                data.Seek(offset, SeekOrigin.Begin);
                bytesRead = data.Read(buf, 0, buf.Length);
                logger.Info(
                    $"OnReadHandle({path}, {info.OpenFlags}, {info.OpenAccess}, {offset}) -> {bytesRead}, {error}");
                return 0;
            }
            catch (Exception e)
            {
                logger.Error(
                    $"OnReadHandle({path}, {info.OpenFlags}, {info.OpenAccess}, {offset}) -> {bytesRead}, error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
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
                var error = fileSystemRepository.TryReadFile(path, info.OpenFlags, out var file);
                if (error != 0)
                {
                    logger.Error($"OnWriteHandle({path}, {info.OpenAccess}, {info.OpenFlags}, {offset}) -> {error}");
                    return error;
                }

                using var dataStream = new MemoryStream();
                if (file.Data.Length > 0)
                {
                    dataStream.Write(file.Data, 0, file.Data.Length);
                }

                dataStream.Seek(offset, SeekOrigin.Begin);
                dataStream.Write(buf, 0, buf.Length);
                bytesWritten = buf.Length;
                file.Data = dataStream.ToArray();
                error = fileSystemRepository.TryWriteFile(file);
                logger.Info(
                    $"OnWriteHandle({path}, {info.OpenAccess}, {info.OpenFlags}, {offset}) -> {bytesWritten}, {error}");
                return 0;
            }
            catch (Exception e)
            {
                logger.Error(
                    $"OnWriteHandle({path}, {info.OpenAccess}, {info.OpenFlags}, {offset}) -> {bytesWritten}, error: {e.Message}, {e.StackTrace}"
                );
                return Errno.ENOENT;
            }
        }

        protected override Errno OnGetFileSystemStatus(string path, out Statvfs stbuf)
        {
            logger.Info($"OnGetFileSystemStatus()");
            try
            {
                var error = fileSystemRepository.TryGetFileSystemStatus(path, out stbuf);
                logger.Info($"OnGetFileSystemStatus({path}, {stbuf}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                stbuf = new Statvfs();
                logger.Error($"OnGetFileSystemStatus({path}, {stbuf}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
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
                var error = fileSystemRepository.TrySetExtendedAttribute(path, name, value, flags);
                logger.Info($"OnSetPathExtendedAttribute({path}, {name}, {flags}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error(
                    $"OnSetPathExtendedAttribute({path}, {name}, {flags}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
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
                var error = fileSystemRepository.TryGetExtendedAttribute(path, name, value, out bytesWritten);
                logger.Info($"OnGetPathExtendedAttribute({path}, {name}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error(
                    $"OnGetPathExtendedAttribute({path}, {name}) -> {value}, error: {e.Message}, {e.StackTrace}");
                bytesWritten = 0;
                return Errno.ENOENT;
            }
        }

        protected override Errno OnListPathExtendedAttributes(string path, out string[] names)
        {
            logger.Info($"OnListPathExtendedAttributes({path})...");
            try
            {
                var error = fileSystemRepository.TryGetExtendedAttributesList(path, out names);
                logger.Info($"OnListPathExtendedAttributes({path}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                names = new string[0];
                logger.Error($"OnListPathExtendedAttributes({path}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
            }
        }

        protected override Errno OnRemovePathExtendedAttribute(string path, string name)
        {
            logger.Info($"OnRemovePathExtendedAttribute({path}, {name})...");

            try
            {
                var error = fileSystemRepository.TryRemoveExtendedAttribute(path, name);
                logger.Info($"OnRemovePathExtendedAttribute({path}, {name}) -> {error}");
                return error;
            }
            catch (Exception e)
            {
                logger.Error($"OnRemovePathExtendedAttribute({path}, {name}) -> error: {e.Message}, {e.StackTrace}");
                return Errno.ENOENT;
            }
        }

        protected override Errno OnLockHandle(string path, OpenedPathInfo info, FcntlCommand cmd, ref Flock @lock)
        {
            logger.Info($"OnLockHandle({path}, {info}, {cmd}, {@lock.l_type})...");
            var error = OnOpenHandle(path, info);
            logger.Info($"OnLockHandle({path}, {info}, {cmd}, {@lock.l_type}) -> {error}");
            return error;
        }
    }
}
