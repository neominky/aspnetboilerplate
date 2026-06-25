using System;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Optional marker for documentation. Generation is enabled automatically when
    /// <c>Abp.SourceGenerators.Runtime</c> is referenced; this attribute is not required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    [Obsolete("Not required. Reference Abp.SourceGenerators.Runtime and Abp.SourceGenerators to enable compile-time registration.")]
    public sealed class AbpCompileTimeRegistrationsAttribute : Attribute
    {
    }
}
