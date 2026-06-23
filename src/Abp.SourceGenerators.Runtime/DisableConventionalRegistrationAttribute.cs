using System;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// When compile-time IoC registration is enabled, types marked with this attribute
    /// are excluded from compile-time assembly scanning and must be registered by generated code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class DisableConventionalRegistrationAttribute : Attribute
    {
    }
}
