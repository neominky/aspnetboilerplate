using System.Collections.Generic;
using System.Reflection;

namespace Abp.Dependency
{
    public sealed class AbpMethodInterceptionMetadataProvider : IAbpMethodInterceptionMetadataProvider
    {
        public static AbpMethodInterceptionMetadataProvider Instance { get; } = new();

        private readonly Dictionary<MethodIdentity, AbpMethodInterceptionMetadata> _metadata = new();

        public void Register(MethodInfo method, AbpMethodInterceptionMetadata metadata)
        {
            Check.NotNull(method, nameof(method));
            Check.NotNull(metadata, nameof(metadata));
            _metadata[MethodIdentity.From(method)] = metadata;
        }

        public bool TryGet(MethodInfo method, out AbpMethodInterceptionMetadata? metadata)
        {
            Check.NotNull(method, nameof(method));

            if (method is AbpMethodInfo abpMethodInfo)
            {
                metadata = abpMethodInfo.Metadata;
                return metadata != null;
            }

            return _metadata.TryGetValue(MethodIdentity.From(method), out metadata);
        }

        internal void Clear()
        {
            _metadata.Clear();
        }

        private readonly struct MethodIdentity : System.IEquatable<MethodIdentity>
        {
            private readonly string _declaringTypeFullName;
            private readonly string _name;
            private readonly string _parameterTypes;

            private MethodIdentity(string declaringTypeFullName, string name, string parameterTypes)
            {
                _declaringTypeFullName = declaringTypeFullName;
                _name = name;
                _parameterTypes = parameterTypes;
            }

            public static MethodIdentity From(MethodInfo method)
            {
                var parameters = method.GetParameters();
                var parameterTypes = parameters.Length == 0
                    ? string.Empty
                    : string.Join(",", System.Array.ConvertAll(parameters, p => p.ParameterType.AssemblyQualifiedName));

                return new MethodIdentity(method.DeclaringType!.FullName!, method.Name, parameterTypes);
            }

            public bool Equals(MethodIdentity other)
            {
                return _declaringTypeFullName == other._declaringTypeFullName
                       && _name == other._name
                       && _parameterTypes == other._parameterTypes;
            }

            public override bool Equals(object? obj) => obj is MethodIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _declaringTypeFullName.GetHashCode();
                    hash = (hash * 397) ^ _name.GetHashCode();
                    hash = (hash * 397) ^ _parameterTypes.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
