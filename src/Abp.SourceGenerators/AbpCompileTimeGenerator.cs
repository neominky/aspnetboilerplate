using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Abp.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class AbpCompileTimeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationProvider = context.CompilationProvider;

        context.RegisterSourceOutput(compilationProvider, static (spc, compilation) =>
        {
            if (!ShouldGenerate(compilation))
            {
                return;
            }

            var models = ConventionModelCollector.Collect(compilation);
            var modules = ModuleCollector.Collect(compilation);

            if (models.IsEmpty || modules.IsEmpty)
            {
                return;
            }

            var userInterceptors = UserInterceptorCollector.Collect(compilation);

            foreach (var module in modules)
            {
                var moduleSource = RegistrationEmitter.EmitModulePartial(module, models, compilation.AssemblyName, userInterceptors);
                if (moduleSource != null)
                {
                    spc.AddSource($"{module.ModuleType.Name}.CompileTime.g.cs", SourceText.From(moduleSource, Encoding.UTF8));
                }
            }

            var initializerSource = RegistrationEmitter.EmitAssemblyInitializer(modules);
            spc.AddSource("CompileTimeIocAssemblyInitializer.g.cs", SourceText.From(initializerSource, Encoding.UTF8));

            foreach (var appService in models.Where(m => m.IsApplicationService))
            {
                var interceptorSource = InterceptorEmitter.Emit(compilation, compilation.AssemblyName, appService);
                if (interceptorSource != null)
                {
                    spc.AddSource($"{appService.InterceptedTypeName}.g.cs", SourceText.From(interceptorSource, Encoding.UTF8));
                }
            }
        });
    }

    private static bool ShouldGenerate(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("Abp.Dependency.CompileTime.CompileTimeInterceptionConfiguration") != null;
    }
}
