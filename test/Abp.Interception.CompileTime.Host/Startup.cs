using Abp.AspNetCore;
using Abp.Interception.CompileTime.Host.Application;
using Abp.Dependency.CompileTime;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Abp.Interception.CompileTime.Host;

public class Startup
{
    public IServiceProvider ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();
        services.AddSingleton<ApplicationPartManager>();

        return services.AddAbp<InterceptionCompileTimeHostModule>(options =>
        {
            CompileTimeInterceptionConfiguration.Enable();
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseAbp(options => options.UseAbpRequestLocalization = false);
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/api/hello", (IHelloAppService helloAppService) => helloAppService.SayHello());
            endpoints.MapGet("/api/hello-value-task", async (IHelloAppService helloAppService) => await helloAppService.SayHelloValueTaskAsync());

            endpoints.MapGet("/api/builtin/audited", (IBuiltInAspectAppService service) => service.GetAuditedMessage());
            endpoints.MapGet("/api/builtin/unit-of-work", (IBuiltInAspectAppService service) => service.GetUnitOfWorkActive());
            endpoints.MapPost("/api/builtin/validate", (IBuiltInAspectAppService service, ValidatedInputDto input) => service.EchoValidated(input));
            endpoints.MapGet("/api/builtin/authorized", (IBuiltInAspectAppService service) => service.GetAuthorizedMessage());
        });
    }
}
