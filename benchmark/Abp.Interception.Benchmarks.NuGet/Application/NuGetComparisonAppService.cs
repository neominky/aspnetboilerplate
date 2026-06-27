using Abp.Application.Services;
using Abp.Auditing;
using Abp.Domain.Uow;
using Abp.Interception.Benchmarks.Contracts;
using Abp.Runtime.Validation;

namespace Abp.Interception.Benchmarks.NuGet.Application;

public interface INuGetComparisonAppService : IApplicationService, IBenchmarkComparisonAppService;

[DisableAuditing]
[UnitOfWork(IsDisabled = true)]
public class NuGetComparisonAppService : ApplicationService, INuGetComparisonAppService
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
