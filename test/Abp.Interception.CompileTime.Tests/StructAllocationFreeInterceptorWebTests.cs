using Abp.Interception.CompileTime.Host;
using Abp.Interception.CompileTime.Host.Interceptors;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Abp.Interception.CompileTime.Tests;

[Collection("CompileTimeHost")]
public class StructAllocationFreeInterceptorWebTests
{
    private readonly WebApplicationFactory<Startup> _factory;

    public StructAllocationFreeInterceptorWebTests(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Api_hello_struct_tagged_sync_should_use_allocation_free_sync_struct_path()
    {
        StructTaggedCompileTimeInterceptor.ResetForTest();
        TaggedCompileTimeInterceptor.ResetForTest();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-tagged");

        Assert.Contains("Hello from struct-tagged sync AppService", response);
        Assert.Contains("[struct-tag:struct-sync]", response);
        Assert.Equal(1, StructTaggedCompileTimeInterceptor.InvocationCount);
        Assert.Equal("struct-sync", StructTaggedCompileTimeInterceptor.LastTag);
    }

    [Fact]
    public async Task Api_hello_struct_tagged_task_should_use_allocation_free_task_struct_path()
    {
        StructTaggedCompileTimeInterceptor.ResetForTest();
        TaggedCompileTimeInterceptor.ResetForTest();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-tagged-task");

        Assert.Contains("Hello from struct-tagged Task AppService", response);
        Assert.Contains("[struct-tag:struct-task]", response);
        Assert.Equal(1, StructTaggedCompileTimeInterceptor.InvocationCount);
        Assert.Equal("struct-task", StructTaggedCompileTimeInterceptor.LastTag);
    }

    [Fact]
    public async Task Api_hello_struct_tagged_value_task_should_use_allocation_free_value_task_struct_path()
    {
        StructTaggedCompileTimeInterceptor.ResetForTest();
        TaggedCompileTimeInterceptor.ResetForTest();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-tagged-value-task");

        Assert.Contains("Hello from struct-tagged ValueTask AppService", response);
        Assert.Contains("[struct-tag:struct-value-task]", response);
        Assert.Equal(1, StructTaggedCompileTimeInterceptor.InvocationCount);
        Assert.Equal("struct-value-task", StructTaggedCompileTimeInterceptor.LastTag);
    }
}
