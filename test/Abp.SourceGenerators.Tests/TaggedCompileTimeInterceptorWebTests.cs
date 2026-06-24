using System.Threading.Tasks;
using Abp.NativeAot.SampleWebApp;
using Abp.NativeAot.SampleWebApp.Interceptors;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Abp.SourceGenerators.Tests;

public class TaggedCompileTimeInterceptorWebTests : IClassFixture<WebApplicationFactory<Startup>>
{
    private readonly WebApplicationFactory<Startup> _factory;

    public TaggedCompileTimeInterceptorWebTests(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Api_hello_should_invoke_user_defined_interceptor_and_apply_tag()
    {
        TaggedCompileTimeInterceptor.ResetForTest();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello");

        Assert.Contains("Hello from compile-time intercepted AppService", response);
        Assert.Equal(1, TaggedCompileTimeInterceptor.InvocationCount);
        Assert.Contains("[tag:demo]", response);
        Assert.Equal(1, TaggedCompileTimeInterceptor.InvocationCount);
        Assert.Equal("demo", TaggedCompileTimeInterceptor.LastTag);
    }

    [Fact]
    public async Task Api_hello_value_task_should_invoke_user_defined_interceptor_and_apply_tag()
    {
        TaggedCompileTimeInterceptor.ResetForTest();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-value-task");

        Assert.Contains("Hello from compile-time intercepted ValueTask AppService", response);
        Assert.Contains("[tag:value-task]", response);
        Assert.Equal(1, TaggedCompileTimeInterceptor.InvocationCount);
        Assert.Equal("value-task", TaggedCompileTimeInterceptor.LastTag);
    }
}
