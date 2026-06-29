using System.Collections.Concurrent;
using Abp.Dependency;
using Abp.Interception.CompileTime.Host;
using Abp.Interception.CompileTime.Host.Application;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abp.Interception.CompileTime.Tests;

[Collection("CompileTimeHost")]
public class CompileTimeInvocationThreadSafetyTests
{
    private const string ClassInvocationMessage = "Hello from compile-time intercepted AppService [tag:demo]";
    private const string MixedSyncMessage = "Hello from struct fast-path sync AppService";
    private const string TaskInvocationMessage = "Hello from compile-time intercepted Task AppService [tag:task]";

    private readonly WebApplicationFactory<Startup> _factory;

    public CompileTimeInvocationThreadSafetyTests(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Concurrent_calls_on_same_instance_should_not_corrupt_invocation_state()
    {
        var resolver = _factory.Services.GetRequiredService<IIocResolver>();
        var service = resolver.Resolve<IHelloAppService>();

        const int workerCount = 32;
        const int iterationsPerWorker = 25;
        var syncResults = new ConcurrentBag<string>();
        var mixedResults = new ConcurrentBag<string>();
        var taskResults = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, workerCount),
            new ParallelOptions { MaxDegreeOfParallelism = workerCount },
            async (_, _) =>
            {
                for (var i = 0; i < iterationsPerWorker; i++)
                {
                    syncResults.Add(service.SayHello());
                    mixedResults.Add(service.SayHelloStructFastPath());
                    taskResults.Add(await service.SayHelloTaskAsync().ConfigureAwait(false));
                }
            });

        var expectedCount = workerCount * iterationsPerWorker;
        Assert.Equal(expectedCount, syncResults.Count);
        Assert.Equal(expectedCount, mixedResults.Count);
        Assert.Equal(expectedCount, taskResults.Count);
        Assert.All(syncResults, result => Assert.Equal(ClassInvocationMessage, result));
        Assert.All(mixedResults, result => Assert.Equal(MixedSyncMessage, result));
        Assert.All(taskResults, result => Assert.Equal(TaskInvocationMessage, result));
    }
}
