using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Dependency.CompileTime;

namespace Abp.Interception.CompileTime.Host.Interceptors;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class TaggedAttribute : Attribute
{
    public string Tag { get; }

    public TaggedAttribute(string tag)
    {
        Tag = tag;
    }
}

[AbpInterceptor(typeof(TaggedAttribute))]
public sealed class TaggedCompileTimeInterceptor : AbpInterceptorBase, ITransientDependency
{
    public static int InvocationCount { get; private set; }

    public static string? LastTag { get; private set; }

    public static void ResetForTest()
    {
        InvocationCount = 0;
        LastTag = null;
    }

    public override void InterceptSynchronous(IAbpInvocation invocation)
    {
        InvocationCount++;

        var tag = TryGetTag(invocation);
        if (tag != null)
        {
            LastTag = tag;
        }

        invocation.Proceed();

        if (tag != null && invocation.ReturnValue is string text)
        {
            invocation.ReturnValue = $"{text} [tag:{tag}]";
        }
    }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation)
    {
        InvocationCount++;

        var tag = TryGetTag(invocation);
        if (tag != null)
        {
            LastTag = tag;
        }

        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        await (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
    {
        InvocationCount++;

        var tag = TryGetTag(invocation);
        if (tag != null)
        {
            LastTag = tag;
        }

        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        var result = await (Task<TResult>)invocation.ReturnValue!;

        if (tag != null && result is string text)
        {
            return (TResult)(object)$"{text} [tag:{tag}]";
        }

        return result;
    }

    private static string? TryGetTag(IAbpInvocation invocation)
    {
        var taggedAttribute = invocation.MethodInvocationTarget
            .GetCustomAttributes(typeof(TaggedAttribute), inherit: true)
            .OfType<TaggedAttribute>()
            .FirstOrDefault();
        if (taggedAttribute != null)
        {
            return taggedAttribute.Tag;
        }

        var targetType = invocation.InvocationTarget.GetType();
        var method = targetType.GetMethod(invocation.MethodInvocationTarget.Name);
        return method?.GetCustomAttribute<TaggedAttribute>()?.Tag;
    }
}
