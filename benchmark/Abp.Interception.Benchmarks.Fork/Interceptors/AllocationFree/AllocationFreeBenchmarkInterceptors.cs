using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Dependency.CompileTime;
using Abp.Interception.Benchmarks.Contracts;

namespace Abp.Interception.Benchmarks.Fork.Interceptors.AllocationFree;

[AbpInterceptor(typeof(BenchmarkAllocationFreeTrigger1Attribute))]
public sealed class AllocationFreeBenchmarkInterceptor1 : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
    {
        BenchmarkInterceptorCounters.RecordAllocationFree1();
        invocation.Proceed();
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>> invocation)
    {
        BenchmarkInterceptorCounters.RecordAllocationFree1();
        return await invocation.Proceed().ConfigureAwait(false);
    }
}

[AbpInterceptor(typeof(BenchmarkAllocationFreeTrigger2Attribute))]
public sealed class AllocationFreeBenchmarkInterceptor2 : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
    {
        BenchmarkInterceptorCounters.RecordAllocationFree2();
        invocation.Proceed();
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>> invocation)
    {
        BenchmarkInterceptorCounters.RecordAllocationFree2();
        return await invocation.Proceed().ConfigureAwait(false);
    }
}

[AbpInterceptor(typeof(BenchmarkAllocationFreeTrigger3Attribute))]
public sealed class AllocationFreeBenchmarkInterceptor3 : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
    {
        BenchmarkInterceptorCounters.RecordAllocationFree3();
        invocation.Proceed();
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>> invocation)
    {
        BenchmarkInterceptorCounters.RecordAllocationFree3();
        return await invocation.Proceed().ConfigureAwait(false);
    }
}
