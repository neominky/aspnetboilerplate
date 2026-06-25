using System.Linq;
using Abp.Dependency;

namespace Abp.EntityHistory
{
    internal class EntityHistoryUseCaseDescriptionProvider : IEntityHistoryUseCaseDescriptionProvider, ITransientDependency
    {
        public string? GetUseCaseDescription(IAbpInvocation invocation)
        {
            var methodInfo = invocation.MethodInvocationTarget;

            if (AbpMethodInfo.TryGetMetadata(methodInfo, out var metadata)
                && !string.IsNullOrEmpty(metadata?.UseCaseDescription))
            {
                return metadata.UseCaseDescription;
            }

            var useCaseAttribute = methodInfo.GetCustomAttributes(typeof(UseCaseAttribute), true).OfType<UseCaseAttribute>().FirstOrDefault()
                                   ?? methodInfo.DeclaringType?.GetCustomAttributes(typeof(UseCaseAttribute), true).OfType<UseCaseAttribute>().FirstOrDefault();

            return useCaseAttribute?.Description;
        }
    }
}
