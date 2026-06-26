using Abp.Interception.CompileTime.Host;
using Abp.Interception.CompileTime.Host.Interceptors;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Abp.Interception.CompileTime.Tests;

[Collection("CompileTimeHost")]
public class TaggedCompileTimeInterceptorWebTests
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
