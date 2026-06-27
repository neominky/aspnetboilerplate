using Abp.Interception.Benchmarks.Fork.Application;
using Abp.Interception.Benchmarks.Fork.Infrastructure;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Abp.Interception.Benchmarks.Fork;

[MemoryDiagnoser]
public class ForkInterceptionBenchmarks
{
    private IClassBridgeComparisonAppService _classBridge = null!;
    private IAllocationFreeComparisonAppService _allocationFree = null!;

    [GlobalSetup]
    public void Setup()
    {
        var bootstrapper = BenchmarkBootstrapperFactory.Create();
        bootstrapper.Initialize();

        _classBridge = bootstrapper.IocManager.Resolve<IClassBridgeComparisonAppService>();
        _allocationFree = bootstrapper.IocManager.Resolve<IAllocationFreeComparisonAppService>();
        ForkInterceptionVerifier.Verify(_classBridge, _allocationFree);
    }

    [Benchmark(Description = "Compile-time (class-bridge)", Baseline = true)]
    public string ClassBridge_Sync() => _classBridge.GetMessage();

    [Benchmark(Description = "Compile-time (allocation-free)")]
    public string AllocationFree_Sync() => _allocationFree.GetMessage();

    [Benchmark(Description = "Compile-time (class-bridge) Task")]
    public async Task<string> ClassBridge_TaskAsync() => await _classBridge.GetMessageTaskAsync();

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
        var classBridge = bootstrapper.IocManager.Resolve<IClassBridgeComparisonAppService>();
        var allocationFree = bootstrapper.IocManager.Resolve<IAllocationFreeComparisonAppService>();
        ForkInterceptionVerifier.Verify(classBridge, allocationFree);
        Console.WriteLine($"Class-bridge type: {classBridge.GetType().FullName}");
        Console.WriteLine($"Allocation-free type: {allocationFree.GetType().FullName}");
        Console.WriteLine("Each scenario: 3 interceptors x 1 invocation per sync/async call (verified)");
    }
}
