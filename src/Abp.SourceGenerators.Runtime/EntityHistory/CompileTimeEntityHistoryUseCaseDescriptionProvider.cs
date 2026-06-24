using System.Linq;
using System.Reflection;
using Abp.Dependency;

namespace Abp.EntityHistory
{
    /// <summary>
    /// Compile-time entity history use case resolution.
    /// Moved from <c>src/Abp/Dependency/AbpBuiltInInvocationExtensions.cs</c>.
    /// </summary>
    internal class CompileTimeEntityHistoryUseCaseDescriptionProvider : IEntityHistoryUseCaseDescriptionProvider
    {
        public string? GetUseCaseDescription(IAbpInvocation invocation)
        {
            if (invocation.MethodInvocationTarget is IAbpBuiltInInterceptionMetadata builtIn
                && !string.IsNullOrEmpty(builtIn.UseCaseDescription))
            {
                return builtIn.UseCaseDescription;
            }

            var methodInfo = invocation.GetMethodInvocationTarget();
            var useCaseAttribute = methodInfo.GetCustomAttributes(typeof(UseCaseAttribute), true).OfType<UseCaseAttribute>().FirstOrDefault()
                                   ?? methodInfo.DeclaringType?.GetCustomAttributes(typeof(UseCaseAttribute), true).OfType<UseCaseAttribute>().FirstOrDefault();

            return useCaseAttribute?.Description;
        }
    }
}
