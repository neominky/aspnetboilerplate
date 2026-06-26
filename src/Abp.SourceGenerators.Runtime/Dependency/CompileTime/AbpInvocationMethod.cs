using System.Reflection;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Stack-friendly method identity for compile-time interception.
    /// Carries baked metadata without allocating an <see cref="AbpMethodInfo"/> wrapper.
    /// </summary>
    public readonly struct AbpInvocationMethod
    {
        public AbpInvocationMethod(MethodInfo method, AbpMethodInterceptionMetadata? metadata)
        {
            Method = method;
            Metadata = metadata;
        }

        public MethodInfo Method { get; }

        public AbpMethodInterceptionMetadata? Metadata { get; }
    }
}
