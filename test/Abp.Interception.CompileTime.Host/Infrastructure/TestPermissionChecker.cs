using System.Threading.Tasks;
using Abp.Authorization;

namespace Abp.Interception.CompileTime.Host.Infrastructure;

public sealed class TestPermissionChecker : IPermissionChecker
{
    public static string? LastPermissionName { get; private set; }

    public static void ResetForTest()
    {
        LastPermissionName = null;
    }

    public bool IsGranted(string permissionName)
    {
        LastPermissionName = permissionName;
        return true;
    }

    public Task<bool> IsGrantedAsync(string permissionName)
    {
        LastPermissionName = permissionName;
        return Task.FromResult(true);
    }

    public bool IsGranted(UserIdentifier user, string permissionName)
    {
        LastPermissionName = permissionName;
        return true;
    }

    public Task<bool> IsGrantedAsync(UserIdentifier user, string permissionName)
    {
        LastPermissionName = permissionName;
        return Task.FromResult(true);
    }
}
