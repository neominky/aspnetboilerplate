using Abp.Interception.Benchmarks.Contracts;
using Abp.Interception.Benchmarks.Fork.Application;

namespace Abp.Interception.Benchmarks.Fork.Infrastructure;

internal static class ForkInterceptionVerifier
{
    public static void Verify(
        IClassInvocationComparisonAppService classInvocation,
        IAllocationFreeComparisonAppService allocationFree)
    {
        VerifyCompileTimeWrapper(classInvocation, nameof(IClassInvocationComparisonAppService));
        InterceptionChainVerifier.VerifyClassInvocation(classInvocation, "Fork class invocation");

        VerifyCompileTimeWrapper(allocationFree, nameof(IAllocationFreeComparisonAppService));
        InterceptionChainVerifier.VerifyAllocationFree(allocationFree, "Fork allocation-free");
    }

    private static void VerifyCompileTimeWrapper(object service, string label)
    {
        var typeName = service.GetType().Name;
        if (!typeName.EndsWith("_Intercepted", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} resolved to '{service.GetType().FullName}', expected a compile-time '*_Intercepted' wrapper.");
        }
    }
}
