using Abp.Application.Services;

namespace Abp.Interception.CompileTime.Host.Application;

public interface IHelloAppService : IApplicationService
{
    string SayHello();

    System.Threading.Tasks.Task<string> SayHelloTaskAsync();

    System.Threading.Tasks.ValueTask<string> SayHelloValueTaskAsync();

    string SayHelloStructTagged();

    System.Threading.Tasks.Task<string> SayHelloStructTaggedTaskAsync();

    System.Threading.Tasks.ValueTask<string> SayHelloStructTaggedValueTaskAsync();

    string SayHelloAuditedAndTagged();
}
