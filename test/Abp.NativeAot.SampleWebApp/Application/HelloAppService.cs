using Abp.Application.Services;

namespace Abp.NativeAot.SampleWebApp.Application;

public class HelloAppService : ApplicationService, IHelloAppService
{
    public string SayHello()
    {
        return "Hello from compile-time intercepted AppService";
    }
}
