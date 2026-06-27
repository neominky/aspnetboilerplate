using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Abp.Interception.CompileTime.Host.Application;
using Abp.Interception.CompileTime.Host.Infrastructure;
using Abp.Interception.CompileTime.Host.Interceptors;
using Abp.Runtime.Validation;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Startup = Abp.Interception.CompileTime.Host.Startup;

namespace Abp.Interception.CompileTime.Tests;

[Collection("CompileTimeHost")]
public class BuiltInInterceptorWebTests
{
    private readonly WebApplicationFactory<Startup> _factory;

    public BuiltInInterceptorWebTests(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Api_builtin_audited_should_save_audit_log()
    {
        TaggedCompileTimeInterceptor.ResetForTest();
        StructFastPathCompileTimeInterceptor.ResetForTest();
        StructCompatCompileTimeInterceptor.ResetForTest();
        TestAuditingStore.ResetForTest();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/builtin/audited");

        Assert.Equal("audited", response);
        Assert.NotNull(TestAuditingStore.LastAudit);
        Assert.Equal(nameof(BuiltInAspectAppService.GetAuditedMessage), TestAuditingStore.LastAudit!.MethodName);
    }

    [Fact]
    public async Task Api_builtin_unit_of_work_should_begin_unit_of_work()
    {
        TaggedCompileTimeInterceptor.ResetForTest();
        StructFastPathCompileTimeInterceptor.ResetForTest();
        StructCompatCompileTimeInterceptor.ResetForTest();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/builtin/unit-of-work");

        Assert.True(bool.Parse(response));
    }

    [Fact]
    public async Task Api_builtin_validate_should_reject_invalid_input()
    {
        TaggedCompileTimeInterceptor.ResetForTest();
        StructFastPathCompileTimeInterceptor.ResetForTest();
        StructCompatCompileTimeInterceptor.ResetForTest();

        var client = _factory.CreateClient();
        using var content = new StringContent("{\"name\":\"\"}", Encoding.UTF8, "application/json");

        await Assert.ThrowsAsync<AbpValidationException>(async () =>
        {
            var response = await client.PostAsync("/api/builtin/validate", content);
            await response.Content.ReadAsStringAsync();
        });
    }

    [Fact]
    public async Task Api_builtin_validate_should_accept_valid_input()
    {
        TaggedCompileTimeInterceptor.ResetForTest();
        StructFastPathCompileTimeInterceptor.ResetForTest();
        StructCompatCompileTimeInterceptor.ResetForTest();

        var client = _factory.CreateClient();
        using var content = new StringContent("{\"name\":\"neo\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/builtin/validate", content);

        response.EnsureSuccessStatusCode();
        Assert.Equal("neo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Api_builtin_authorized_should_check_permission()
    {
        TaggedCompileTimeInterceptor.ResetForTest();
        StructFastPathCompileTimeInterceptor.ResetForTest();
        StructCompatCompileTimeInterceptor.ResetForTest();
        TestPermissionChecker.ResetForTest();

        var client = _factory.CreateClient();
        var response = await client.GetStringAsync("/api/builtin/authorized");

        Assert.Equal("authorized", response);
        Assert.Equal("Sample.Permission", TestPermissionChecker.LastPermissionName);
    }
}
