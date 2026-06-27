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
    public async Task Api_hello_struct_fast_path_sync_should_use_proceed_fast_path()
    {
        ResetStructInterceptors();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-fast-path");

        Assert.Contains("Hello from struct fast-path sync AppService", response);
        Assert.Equal(1, StructFastPathCompileTimeInterceptor.InvocationCount);
        Assert.Equal("fast-sync", StructFastPathCompileTimeInterceptor.LastTag);
        Assert.Equal(0, StructCompatCompileTimeInterceptor.InvocationCount);
    }

    [Fact]
    public async Task Api_hello_struct_fast_path_task_should_use_proceed_fast_path()
    {
        ResetStructInterceptors();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-fast-path-task");

        Assert.Contains("Hello from struct fast-path Task AppService", response);
        Assert.Equal(1, StructFastPathCompileTimeInterceptor.InvocationCount);
        Assert.Equal("fast-task", StructFastPathCompileTimeInterceptor.LastTag);
        Assert.Equal(0, StructCompatCompileTimeInterceptor.InvocationCount);
    }

    [Fact]
    public async Task Api_hello_struct_fast_path_value_task_should_use_proceed_fast_path()
    {
        ResetStructInterceptors();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-fast-path-value-task");

        Assert.Contains("Hello from struct fast-path ValueTask AppService", response);
        Assert.Equal(1, StructFastPathCompileTimeInterceptor.InvocationCount);
        Assert.Equal("fast-value-task", StructFastPathCompileTimeInterceptor.LastTag);
        Assert.Equal(0, StructCompatCompileTimeInterceptor.InvocationCount);
    }

    [Fact]
    public async Task Api_hello_struct_compat_sync_should_use_capture_proceed_info_pattern()
    {
        ResetStructInterceptors();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-compat");

        Assert.Contains("Hello from struct compat sync AppService", response);
        Assert.Equal(0, StructFastPathCompileTimeInterceptor.InvocationCount);
        Assert.Equal(1, StructCompatCompileTimeInterceptor.InvocationCount);
        Assert.Equal("compat-sync", StructCompatCompileTimeInterceptor.LastTag);
    }

    [Fact]
    public async Task Api_hello_struct_compat_task_should_use_capture_proceed_info_pattern()
    {
        ResetStructInterceptors();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-compat-task");

        Assert.Contains("Hello from struct compat Task AppService", response);
        Assert.Equal(0, StructFastPathCompileTimeInterceptor.InvocationCount);
        Assert.Equal(1, StructCompatCompileTimeInterceptor.InvocationCount);
        Assert.Equal("compat-task", StructCompatCompileTimeInterceptor.LastTag);
    }

    [Fact]
    public async Task Api_hello_struct_compat_value_task_should_use_capture_proceed_info_pattern()
    {
        ResetStructInterceptors();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/hello-struct-compat-value-task");

        Assert.Contains("Hello from struct compat ValueTask AppService", response);
        Assert.Equal(0, StructFastPathCompileTimeInterceptor.InvocationCount);
        Assert.Equal(1, StructCompatCompileTimeInterceptor.InvocationCount);
        Assert.Equal("compat-value-task", StructCompatCompileTimeInterceptor.LastTag);
    }

    private static void ResetStructInterceptors()
    {
        StructFastPathCompileTimeInterceptor.ResetForTest();
        StructCompatCompileTimeInterceptor.ResetForTest();
        TaggedCompileTimeInterceptor.ResetForTest();
    }
}
