using Abp.AspNetCore;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Interception.CompileTime.Host.Infrastructure;
using Abp.Configuration.Startup;
using Abp.Dependency.CompileTime;
using Abp.Modules;
using Abp.Runtime.Session;

namespace Abp.Interception.CompileTime.Host;

[DependsOn(typeof(AbpAspNetCoreModule))]
public partial class InterceptionCompileTimeHostModule : AbpModule
{
    public override void PreInitialize()
    {
        Configuration.Auditing.IsEnabled = true;
        Configuration.Auditing.IsEnabledForAnonymousUsers = true;

        Configuration.ReplaceService<IAuditingStore, TestAuditingStore>();
        Configuration.ReplaceService<IAbpSession, TestAbpSession>();
        Configuration.ReplaceService<IPermissionChecker, TestPermissionChecker>();
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(InterceptionCompileTimeHostModule));
    }
}
