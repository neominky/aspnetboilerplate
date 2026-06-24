using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Abp.Dependency
{
    /// <summary>
    /// <see cref="AbpMethodInfo"/> backed by reflection.
    /// Shared by Castle bridge (<c>Abp.Interception.Castle</c>) and compile-time metadata; remains in <c>Abp</c>.
    /// </summary>
    public sealed class ReflectionAbpMethodInfo : AbpMethodInfo
    {
        private readonly MethodInfo _methodInfo;
        private readonly IReadOnlyList<AbpParameterInfo> _parameters;

        private ReflectionAbpMethodInfo(MethodInfo methodInfo)
        {
            _methodInfo = methodInfo;
            _parameters = methodInfo
                .GetParameters()
                .Select(p => new AbpParameterInfo(
                    p.Name!,
                    p.ParameterType,
                    p.Position,
                    p.IsOptional,
                    p.HasDefaultValue,
                    p.HasDefaultValue ? p.DefaultValue : null))
                .ToArray();
        }

        public override string Name => _methodInfo.Name;

        public override Type DeclaringType => _methodInfo.DeclaringType!;

        public override Type ReturnType => _methodInfo.ReturnType;

        public override bool IsPublic => _methodInfo.IsPublic;

        public override IReadOnlyList<AbpParameterInfo> Parameters => _parameters;

        public override MethodInfo? ReflectionMethod => _methodInfo;

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return _methodInfo.IsDefined(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return _methodInfo.GetCustomAttributes(inherit);
        }

        public static ReflectionAbpMethodInfo From(MethodInfo methodInfo)
        {
            return new ReflectionAbpMethodInfo(methodInfo);
        }
    }
}
