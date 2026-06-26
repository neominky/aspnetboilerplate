using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Auditing;
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
    [Tagged("task")]
    public async Task<string> SayHelloTaskAsync()
    {
        await Task.Yield();
        return "Hello from compile-time intercepted Task AppService";
    }

    [DisableValidation]
    [Tagged("value-task")]
    public async ValueTask<string> SayHelloValueTaskAsync()
    {
        await Task.Yield();
        return "Hello from compile-time intercepted ValueTask AppService";
    }

    [DisableValidation]
    [StructTagged("struct-sync")]
    public string SayHelloStructTagged()
    {
        return "Hello from struct-tagged sync AppService";
    }

    [DisableValidation]
    [StructTagged("struct-task")]
    public async Task<string> SayHelloStructTaggedTaskAsync()
    {
        await Task.Yield();
        return "Hello from struct-tagged Task AppService";
    }

    [DisableValidation]
    [StructTagged("struct-value-task")]
    public async ValueTask<string> SayHelloStructTaggedValueTaskAsync()
    {
        await Task.Yield();
        return "Hello from struct-tagged ValueTask AppService";
    }

    [Audited]
    [DisableValidation]
    [Tagged("audited-demo")]
    public string SayHelloAuditedAndTagged()
    {
        return "Hello from audited and tagged AppService";
    }
}
