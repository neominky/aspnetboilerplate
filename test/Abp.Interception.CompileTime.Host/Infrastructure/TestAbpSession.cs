using System;
using Abp.MultiTenancy;
using Abp.Runtime.Session;

namespace Abp.Interception.CompileTime.Host.Infrastructure;

public sealed class TestAbpSession : IAbpSession
{
    public long? UserId => 1;

    public int? TenantId => null;

    public long? ImpersonatorUserId => null;

    public int? ImpersonatorTenantId => null;

    public MultiTenancySides MultiTenancySide => MultiTenancySides.Tenant;

    public IDisposable Use(int? tenantId, long? userId)
    {
        return NullDisposable.Instance;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
