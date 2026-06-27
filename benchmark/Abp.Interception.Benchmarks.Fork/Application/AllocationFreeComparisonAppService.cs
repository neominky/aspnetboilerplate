using Abp.Application.Services;
using Abp.Auditing;
using Abp.Domain.Uow;
using Abp.Interception.Benchmarks.Contracts;
using Abp.Runtime.Validation;

namespace Abp.Interception.Benchmarks.Fork.Application;

public interface IAllocationFreeComparisonAppService : IApplicationService, IBenchmarkComparisonAppService;

[DisableAuditing]
[UnitOfWork(IsDisabled = true)]
public class AllocationFreeComparisonAppService : ApplicationService, IAllocationFreeComparisonAppService
{
    [DisableValidation]
    [BenchmarkAllocationFreeTrigger1]
    [BenchmarkAllocationFreeTrigger2]
    [BenchmarkAllocationFreeTrigger3]
    public string GetMessage()
    {
        return "benchmark";
    }

    [DisableValidation]
    [BenchmarkAllocationFreeTrigger1]
    [BenchmarkAllocationFreeTrigger2]
    [BenchmarkAllocationFreeTrigger3]
    public async Task<string> GetMessageTaskAsync()
    {
        await Task.Yield();
        return "benchmark";
    }
}
