using Abp.Dependency;
using Abp.Interception.Benchmarks.Contracts;
using Abp.Interception.Benchmarks.NuGet.Application;
using Castle.Core;

namespace Abp.Interception.Benchmarks.NuGet.Infrastructure;

internal static class NuGetInterceptionVerifier
{
    public static void Verify(IIocManager iocManager, INuGetComparisonAppService appService)
    {
        VerifyHandlerHasInterceptors(iocManager);
        InterceptionChainVerifier.VerifyNuGet(appService, "NuGet Castle", requireCastleProxy: true);
    }

    private static void VerifyHandlerHasInterceptors(IIocManager iocManager)
    {
        var handler = iocManager.IocContainer.Kernel.GetHandler(typeof(INuGetComparisonAppService));
        if (handler == null)
        {
            throw new InvalidOperationException("INuGetComparisonAppService is not registered in Windsor.");
        }

        var interceptorCount = handler.ComponentModel.Interceptors.Count;
        if (interceptorCount < 3)
        {
            throw new InvalidOperationException(
                $"INuGetComparisonAppService has {interceptorCount} Windsor interceptor(s), expected 3.");
        }
    }
}
