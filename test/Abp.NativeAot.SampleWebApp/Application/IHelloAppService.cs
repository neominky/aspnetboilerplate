using Abp.Application.Services;

namespace Abp.NativeAot.SampleWebApp.Application;

public interface IHelloAppService : IApplicationService
{
    string SayHello();
}
