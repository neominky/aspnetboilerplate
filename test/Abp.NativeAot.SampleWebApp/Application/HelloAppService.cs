using Abp.Application.Services;
using Abp.NativeAot.SampleWebApp.Interceptors;
using Abp.Runtime.Validation;

namespace Abp.NativeAot.SampleWebApp.Application;

public class HelloAppService : ApplicationService, IHelloAppService
{
    [DisableValidation]
    [Tagged("demo")]
    public string SayHello()
    {
        return "Hello from compile-time intercepted AppService";
    }

    [DisableValidation]
    [Tagged("value-task")]
    public async System.Threading.Tasks.ValueTask<string> SayHelloValueTaskAsync()
    {
        await System.Threading.Tasks.Task.Yield();
        return "Hello from compile-time intercepted ValueTask AppService";
    }
}
