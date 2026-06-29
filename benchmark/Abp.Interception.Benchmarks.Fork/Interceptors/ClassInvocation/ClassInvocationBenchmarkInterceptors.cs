using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Dependency.CompileTime;
using Abp.Interception.Benchmarks.Contracts;

namespace Abp.Interception.Benchmarks.Fork.Interceptors.ClassInvocation;

[AbpInterceptor(typeof(BenchmarkTrigger1Attribute))]
public sealed class ClassInvocationBenchmarkInterceptor1 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation1();
        invocation.Proceed();
    }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation1();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        await (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation1();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}

[AbpInterceptor(typeof(BenchmarkTrigger2Attribute))]
public sealed class ClassInvocationBenchmarkInterceptor2 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation2();
        invocation.Proceed();
    }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation2();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        await (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation2();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}

[AbpInterceptor(typeof(BenchmarkTrigger3Attribute))]
public sealed class ClassInvocationBenchmarkInterceptor3 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation3();
        invocation.Proceed();
    }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation3();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        await (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordClassInvocation3();
        var proceedInfo = invocation.CaptureProceedInfo();
        proceedInfo.Invoke();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}
