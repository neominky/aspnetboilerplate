namespace Abp.Interception.Benchmarks.Contracts;

public interface IBenchmarkComparisonAppService
{
    string GetMessage();

    Task<string> GetMessageTaskAsync();
}
