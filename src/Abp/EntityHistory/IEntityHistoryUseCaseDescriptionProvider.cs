using Abp.Dependency;

namespace Abp.EntityHistory
{
    internal interface IEntityHistoryUseCaseDescriptionProvider
    {
        string? GetUseCaseDescription(IAbpInvocation invocation);
    }
}
