using System.Reflection;

namespace Abp.Dependency
{
    /// <summary>
    /// Reflection helpers for built-in interceptors (runtime Castle and compile-time paths).
    /// </summary>
    public static class AbpInvocationExtensions
    {
        public static MethodInfo GetMethodInvocationTarget(this IAbpInvocation invocation)
        {
            return invocation.MethodInvocationTarget;
        }

        public static MethodInfo GetAbpMethod(this IAbpInvocation invocation)
        {
            return invocation.MethodInvocationTarget;
        }
    }
}
