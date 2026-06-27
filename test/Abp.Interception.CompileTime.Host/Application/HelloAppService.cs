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
    [StructFastPathTagged("fast-sync")]
    public string SayHelloStructFastPath()
    {
        return "Hello from struct fast-path sync AppService";
    }

    [DisableValidation]
    [StructFastPathTagged("fast-task")]
    public async Task<string> SayHelloStructFastPathTaskAsync()
    {
        await Task.Yield();
        return "Hello from struct fast-path Task AppService";
    }

    [DisableValidation]
    [StructFastPathTagged("fast-value-task")]
    public async ValueTask<string> SayHelloStructFastPathValueTaskAsync()
    {
        await Task.Yield();
        return "Hello from struct fast-path ValueTask AppService";
    }

    [DisableValidation]
    [StructCompatTagged("compat-sync")]
    public string SayHelloStructCompat()
    {
        return "Hello from struct compat sync AppService";
    }

    [DisableValidation]
    [StructCompatTagged("compat-task")]
    public async Task<string> SayHelloStructCompatTaskAsync()
    {
        await Task.Yield();
        return "Hello from struct compat Task AppService";
    }

    [DisableValidation]
    [StructCompatTagged("compat-value-task")]
    public async ValueTask<string> SayHelloStructCompatValueTaskAsync()
    {
        await Task.Yield();
        return "Hello from struct compat ValueTask AppService";
    }

    [Audited]
    [DisableValidation]
    [Tagged("audited-demo")]
    public string SayHelloAuditedAndTagged()
    {
        return "Hello from audited and tagged AppService";
    }
}
