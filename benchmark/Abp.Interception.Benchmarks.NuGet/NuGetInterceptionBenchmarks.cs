using Abp.Interception.Benchmarks.Contracts;
using Abp.Interception.Benchmarks.NuGet.Application;
using Abp.Interception.Benchmarks.NuGet.Infrastructure;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Abp.Interception.Benchmarks.NuGet;

[MemoryDiagnoser]
public class NuGetInterceptionBenchmarks
{
    private INuGetComparisonAppService _appService = null!;

    [GlobalSetup]
    public void Setup()
    {
        var bootstrapper = BenchmarkBootstrapperFactory.Create();
        bootstrapper.Initialize();
        _appService = bootstrapper.IocManager.Resolve<INuGetComparisonAppService>();
        NuGetInterceptionVerifier.Verify(bootstrapper.IocManager, _appService);
    }

    [Benchmark(Description = "NuGet Abp (Castle DynamicProxy)")]
    public string Castle_Sync() => _appService.GetMessage();

    [Benchmark(Description = "NuGet Abp (Castle DynamicProxy) Task")]
    public async Task<string> Castle_TaskAsync() => await _appService.GetMessageTaskAsync();
}

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains("--verify", StringComparer.OrdinalIgnoreCase))
        {
            RunVerification();
            Console.WriteLine("NuGet interception chain verification passed.");
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    private static void RunVerification()
    {
        var bootstrapper = BenchmarkBootstrapperFactory.Create();
        bootstrapper.Initialize();
        var appService = bootstrapper.IocManager.Resolve<INuGetComparisonAppService>();
        NuGetInterceptionVerifier.Verify(bootstrapper.IocManager, appService);
        Console.WriteLine($"Resolved type: {appService.GetType().FullName}");
        Console.WriteLine($"Castle proxy: {InterceptionChainVerifier.IsCastleProxy(appService)}");
        Console.WriteLine("Interceptor invocations per sync call: #1=1, #2=1, #3=1 (verified)");
    }
}
