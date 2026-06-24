using Abp.AspNetCore;
using Abp.Dependency.CompileTime;
using Abp.NativeAot.SampleWebApp.Application;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Abp.NativeAot.SampleWebApp;

public class Startup
{
    public IServiceProvider ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();
        services.AddSingleton<ApplicationPartManager>();

        return services.AddAbp<NativeAotSampleWebAppModule>(options =>
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
        });
    }
}
