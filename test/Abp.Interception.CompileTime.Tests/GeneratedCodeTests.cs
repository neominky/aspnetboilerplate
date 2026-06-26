using System.Reflection;
using Abp.Interception.CompileTime.Host;
using Abp.Dependency.CompileTime;
using Xunit;

namespace Abp.Interception.CompileTime.Tests;

public class GeneratedCodeTests
{
    [Fact]
    public void Sample_module_should_have_generated_RegisterAssemblyByConvention_partial()
    {
        var method = typeof(InterceptionCompileTimeHostModule).GetMethod(
            nameof(InterceptionCompileTimeHostModule.RegisterAssemblyByConvention),
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Abp.Dependency.IIocManager) },
            null);

        Assert.NotNull(method);
    }

    [Fact]
    public void CompileTime_RegisterAssemblyByConvention_extension_should_exist()
    {
        var method = typeof(CompileTimeIocManagerExtensions).GetMethod(
            nameof(CompileTimeIocManagerExtensions.RegisterAssemblyByConvention),
            new[] { typeof(Abp.Dependency.IIocManager), typeof(System.Type) });

        Assert.NotNull(method);
    }
}
