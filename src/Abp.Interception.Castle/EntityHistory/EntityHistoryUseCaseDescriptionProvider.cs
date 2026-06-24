using System.Linq;
using Abp.Dependency;

namespace Abp.EntityHistory
{
    internal class EntityHistoryUseCaseDescriptionProvider : IEntityHistoryUseCaseDescriptionProvider
    {
        public string? GetUseCaseDescription(IAbpInvocation invocation)
        {
            var methodInfo = invocation.GetMethodInvocationTarget();
            var useCaseAttribute = methodInfo.GetCustomAttributes(typeof(UseCaseAttribute), true).OfType<UseCaseAttribute>().FirstOrDefault()
                                   ?? methodInfo.DeclaringType?.GetCustomAttributes(typeof(UseCaseAttribute), true).OfType<UseCaseAttribute>().FirstOrDefault();

            return useCaseAttribute?.Description;
        }
    }
}
