using System;

namespace Abp.Dependency
{
    /// <summary>
    /// Compile-time or reflection-backed method parameter metadata.
    /// </summary>
    public sealed class AbpParameterInfo
    {
        public AbpParameterInfo(
            string name,
            Type parameterType,
            int position,
            bool isOptional = false,
            bool hasDefaultValue = false,
            object? defaultValue = null)
        {
            Name = name;
            ParameterType = parameterType;
            Position = position;
            IsOptional = isOptional;
            HasDefaultValue = hasDefaultValue;
            DefaultValue = defaultValue;
        }

        public string Name { get; }

        public Type ParameterType { get; }

        public int Position { get; }

        public bool IsOptional { get; }

        public bool HasDefaultValue { get; }

        public object? DefaultValue { get; }
    }
}
