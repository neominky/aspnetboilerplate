using Abp.Application.Services;
using Abp.Interception.CompileTime.Host.Interceptors;
using Abp.Runtime.Validation;

namespace Abp.Interception.CompileTime.Host.Application;

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
