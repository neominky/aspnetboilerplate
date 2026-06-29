using Abp.Interception.Benchmarks.Fork.Application;
using Abp.Interception.Benchmarks.Fork.Infrastructure;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Abp.Interception.Benchmarks.Fork;

[MemoryDiagnoser]
public class ForkInterceptionBenchmarks
{
    private IClassInvocationComparisonAppService _classInvocation = null!;
    private IAllocationFreeComparisonAppService _allocationFree = null!;

    [GlobalSetup]
    public void Setup()
    {
        var bootstrapper = BenchmarkBootstrapperFactory.Create();
        bootstrapper.Initialize();

        _classInvocation = bootstrapper.IocManager.Resolve<IClassInvocationComparisonAppService>();
        _allocationFree = bootstrapper.IocManager.Resolve<IAllocationFreeComparisonAppService>();
        ForkInterceptionVerifier.Verify(_classInvocation, _allocationFree);
    }

    [Benchmark(Description = "Compile-time (class invocation)", Baseline = true)]
    public string ClassInvocation_Sync() => _classInvocation.GetMessage();

    [Benchmark(Description = "Compile-time (allocation-free)")]
    public string AllocationFree_Sync() => _allocationFree.GetMessage();

    [Benchmark(Description = "Compile-time (class invocation) Task")]
    public async Task<string> ClassInvocation_TaskAsync() => await _classInvocation.GetMessageTaskAsync();

    [Benchmark(Description = "Compile-time (allocation-free) Task")]
    public async Task<string> AllocationFree_TaskAsync() => await _allocationFree.GetMessageTaskAsync();
}

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains("--verify", StringComparer.OrdinalIgnoreCase))
        {
            RunVerification();
            Console.WriteLine("Fork interception chain verification passed.");
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    private static void RunVerification()
    {
        var bootstrapper = BenchmarkBootstrapperFactory.Create();
        bootstrapper.Initialize();
        var classInvocation = bootstrapper.IocManager.Resolve<IClassInvocationComparisonAppService>();
        var allocationFree = bootstrapper.IocManager.Resolve<IAllocationFreeComparisonAppService>();
        ForkInterceptionVerifier.Verify(classInvocation, allocationFree);
        Console.WriteLine($"Class invocation type: {classInvocation.GetType().FullName}");
        Console.WriteLine($"Allocation-free type: {allocationFree.GetType().FullName}");
        Console.WriteLine("Each scenario: 3 interceptors x 1 invocation per sync/async call (verified)");
    }
}
