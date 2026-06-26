using Abp.Application.Services;

namespace Abp.Interception.CompileTime.Host.Application;

public interface IHelloAppService : IApplicationService
{
    string SayHello();

    System.Threading.Tasks.ValueTask<string> SayHelloValueTaskAsync();
}
