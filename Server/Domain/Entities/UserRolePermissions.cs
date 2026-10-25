namespace ShadowverseBeyond.Server.Domain.Entities;

public static class UserRolePermissions
{
    public static int GetPermissionLevel(UserRole role) => role switch
    {
        UserRole.Admin => 100,
        UserRole.Player => 10,
        UserRole.Guest => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported user role.")
    };
}
