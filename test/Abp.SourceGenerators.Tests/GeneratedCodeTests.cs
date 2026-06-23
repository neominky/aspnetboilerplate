using System.Reflection;
using Abp.Dependency.CompileTime;
using Abp.NativeAot.SampleWebApp;
using Xunit;

namespace Abp.SourceGenerators.Tests;

public class GeneratedCodeTests
{
    [Fact]
    public void SampleWebApp_module_should_have_generated_RegisterAssemblyByConvention_partial()
    {
        var method = typeof(NativeAotSampleWebAppModule).GetMethod(
            nameof(NativeAotSampleWebAppModule.RegisterAssemblyByConvention),
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
