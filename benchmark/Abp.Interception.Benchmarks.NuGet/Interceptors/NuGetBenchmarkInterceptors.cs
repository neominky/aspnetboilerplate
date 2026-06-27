using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Interception.Benchmarks.Contracts;
using Castle.DynamicProxy;

namespace Abp.Interception.Benchmarks.NuGet.Interceptors;

public sealed class NuGetBenchmarkInterceptor1 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet1();
        invocation.Proceed();
    }

    protected override Task InternalInterceptAsynchronous(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet1();
        invocation.Proceed();
        return (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet1();
        invocation.Proceed();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}

public sealed class NuGetBenchmarkInterceptor2 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet2();
        invocation.Proceed();
    }

    protected override Task InternalInterceptAsynchronous(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet2();
        invocation.Proceed();
        return (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet2();
        invocation.Proceed();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}

public sealed class NuGetBenchmarkInterceptor3 : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet3();
        invocation.Proceed();
    }

    protected override Task InternalInterceptAsynchronous(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet3();
        invocation.Proceed();
        return (Task)invocation.ReturnValue!;
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
    {
        BenchmarkInterceptorCounters.RecordNuGet3();
        invocation.Proceed();
        return await (Task<TResult>)invocation.ReturnValue!;
    }
}
