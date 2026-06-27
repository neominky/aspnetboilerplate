using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Dependency.CompileTime;
using Abp.Interception.Benchmarks.Contracts;

namespace Abp.Interception.Benchmarks.Fork.Interceptors.ClassBridge;

[AbpInterceptor(typeof(BenchmarkTrigger1Attribute))]
public sealed class ClassBridgeBenchmarkInterceptor1 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge1();
        invocation.Proceed();
    }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge1();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        await (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge1();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}

[AbpInterceptor(typeof(BenchmarkTrigger2Attribute))]
public sealed class ClassBridgeBenchmarkInterceptor2 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge2();
        invocation.Proceed();
    }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge2();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        await (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge2();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}

[AbpInterceptor(typeof(BenchmarkTrigger3Attribute))]
public sealed class ClassBridgeBenchmarkInterceptor3 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge3();
        invocation.Proceed();
    }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge3();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        await (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassBridge3();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}
