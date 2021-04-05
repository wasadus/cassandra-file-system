using CassandraFS.Models;
using Mono.Unix.Native;

namespace CassandraFS
{
    public static class FilePermissionsExtensions
    {
        public static bool CanUserRead(this FilePermissions permissions, uint userUID, uint userGID, uint fileUID, uint fileGID) =>
            userUID == 0
            || (userUID == fileUID && (permissions & FilePermissions.S_IRUSR) != 0)
            || (userGID == fileGID && (permissions & FilePermissions.S_IRGRP) != 0)
            || (permissions & FilePermissions.S_IROTH) != 0;

        public static bool CanUserWrite(this FilePermissions permissions, uint userUID, uint userGID, uint fileUID, uint fileGID) =>
            userUID == 0
            || (userUID == fileUID && (permissions & FilePermissions.S_IWUSR) != 0)
            || (userGID == fileGID && (permissions & FilePermissions.S_IWGRP) != 0)
            || (permissions & FilePermissions.S_IWOTH) != 0;

        public static bool CanUserExecute(this FilePermissions permissions, uint userUID, uint userGID, uint fileUID, uint fileGID) =>
            userUID == 0
            || (userUID == fileUID && (permissions & FilePermissions.S_IXUSR) != 0)
            || (userGID == fileGID && (permissions & FilePermissions.S_IXGRP) != 0)
            || (permissions & FilePermissions.S_IXOTH) != 0;

        public static bool IsChownPermissionsOk(this IFileSystemEntry entry, uint newUID, uint newGID)
        {
            var userUID = Syscall.getuid();
            return userUID == 0 || (entry.UID == newUID && (entry.GID == newGID || userUID == entry.UID));
        }
    }
}