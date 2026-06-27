using Abp.Application.Services;
using Abp.Auditing;
using Abp.Domain.Uow;
using Abp.Interception.Benchmarks.Contracts;
using Abp.Runtime.Validation;

namespace Abp.Interception.Benchmarks.Fork.Application;

public interface IClassBridgeComparisonAppService : IApplicationService, IBenchmarkComparisonAppService;

[DisableAuditing]
[UnitOfWork(IsDisabled = true)]
public class ClassBridgeComparisonAppService : ApplicationService, IClassBridgeComparisonAppService
{
    [DisableValidation]
    [BenchmarkTrigger1]
    [BenchmarkTrigger2]
    [BenchmarkTrigger3]
    public string GetMessage()
    {
        return "benchmark";
    }

    [DisableValidation]
    [BenchmarkTrigger1]
    [BenchmarkTrigger2]
    [BenchmarkTrigger3]
    public async Task<string> GetMessageTaskAsync()
    {
        await Task.Yield();
        return "benchmark";
    }
}
