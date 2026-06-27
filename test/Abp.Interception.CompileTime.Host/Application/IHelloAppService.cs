using Abp.Application.Services;

namespace Abp.Interception.CompileTime.Host.Application;

public interface IHelloAppService : IApplicationService
{
    string SayHello();

    System.Threading.Tasks.Task<string> SayHelloTaskAsync();

    System.Threading.Tasks.ValueTask<string> SayHelloValueTaskAsync();

    string SayHelloStructFastPath();

    System.Threading.Tasks.Task<string> SayHelloStructFastPathTaskAsync();

    System.Threading.Tasks.ValueTask<string> SayHelloStructFastPathValueTaskAsync();

    string SayHelloStructCompat();

    System.Threading.Tasks.Task<string> SayHelloStructCompatTaskAsync();

    System.Threading.Tasks.ValueTask<string> SayHelloStructCompatValueTaskAsync();

    string SayHelloAuditedAndTagged();
}
