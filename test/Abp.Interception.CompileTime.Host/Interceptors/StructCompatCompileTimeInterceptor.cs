using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Dependency.CompileTime;

namespace Abp.Interception.CompileTime.Host.Interceptors;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class StructCompatTaggedAttribute : Attribute
{
    public string Tag { get; }

    public StructCompatTaggedAttribute(string tag)
    {
        Tag = tag;
    }
}

/// <summary>
/// Allocation-free interceptor using the <see cref="IAbpInvocation"/> compatibility pattern:
/// <see cref="AbpInvocationStruct{TAsync}.CaptureProceedInfo"/> before work, then
/// <see cref="AbpStructProceedInfo{TAsync}.Invoke"/> (same shape as built-in interceptors).
/// </summary>
[AbpInterceptor(typeof(StructCompatTaggedAttribute))]
public sealed class StructCompatCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    public static int InvocationCount { get; private set; }

    public static string? LastTag { get; private set; }

    public static void ResetForTest()
    {
        InvocationCount = 0;
        LastTag = null;
    }

    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
    {
        RecordTagIfPresent(invocation.Method, invocation.InvocationTarget);
        invocation.Proceed();
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>> invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();

        RecordTagIfPresent(invocation.Method, invocation.InvocationTarget);

        var taskResult = proceedInfo.Invoke();
        return await taskResult.ConfigureAwait(false);
    }

    protected override async ValueTask<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<ValueTask<TResult>> invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();

        RecordTagIfPresent(invocation.Method, invocation.InvocationTarget);

        var valueTaskResult = proceedInfo.Invoke();
        return await valueTaskResult.ConfigureAwait(false);
    }

    private static void RecordTagIfPresent(MethodInfo methodInvocationTarget, object invocationTarget)
    {
        var tag = TryGetTag(methodInvocationTarget, invocationTarget);
        if (tag == null)
        {
            return;
        }

        InvocationCount++;
        LastTag = tag;
    }

    private static string? TryGetTag(MethodInfo methodInvocationTarget, object invocationTarget)
    {
        var taggedAttribute = methodInvocationTarget
            .GetCustomAttributes(typeof(StructCompatTaggedAttribute), inherit: true)
            .OfType<StructCompatTaggedAttribute>()
            .FirstOrDefault();
        if (taggedAttribute != null)
        {
            return taggedAttribute.Tag;
        }

        var targetType = invocationTarget.GetType();
        var method = targetType.GetMethod(methodInvocationTarget.Name);
        return method?.GetCustomAttribute<StructCompatTaggedAttribute>()?.Tag;
    }
}
