using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Abp.Dependency
{
    /// <summary>
    /// <see cref="MethodInfo"/> that carries baked interception metadata from
    /// <see cref="AbpMethodInterceptionMetadataProvider"/>.
    /// </summary>
    public sealed class AbpMethodInfo : MethodInfo
    {
        private readonly MethodInfo _inner;

        private AbpMethodInfo(MethodInfo inner, AbpMethodInterceptionMetadata metadata)
        {
            _inner = inner;
            Metadata = metadata;
        }

        public AbpMethodInterceptionMetadata Metadata { get; }

        public static MethodInfo GetInvocationMethod(MethodInfo method)
        {
            Check.NotNull(method, nameof(method));

            if (method is AbpMethodInfo)
            {
                return method;
            }

            if (AbpMethodInterceptionMetadataProvider.Instance.TryGet(method, out var metadata)
                && metadata != null)
            {
                return new AbpMethodInfo(method, metadata);
            }

            return method;
        }

        public static bool TryGetMetadata(MethodInfo method, out AbpMethodInterceptionMetadata? metadata)
        {
            if (method is AbpMethodInfo abpMethodInfo)
            {
                metadata = abpMethodInfo.Metadata;
                return metadata != null;
            }

            return AbpMethodInterceptionMetadataProvider.Instance.TryGet(method, out metadata)
                   && metadata != null;
        }

        public override MemberTypes MemberType => _inner.MemberType;

        public override string Name => _inner.Name;

        public override Type? DeclaringType => _inner.DeclaringType;

        public override Type? ReflectedType => _inner.ReflectedType;

        public override int MetadataToken => _inner.MetadataToken;

        public override Module Module => _inner.Module;

        public override RuntimeMethodHandle MethodHandle => _inner.MethodHandle;

        public override MethodAttributes Attributes => _inner.Attributes;

        public override CallingConventions CallingConvention => _inner.CallingConvention;

        public override Type ReturnType => _inner.ReturnType;

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => _inner.ReturnTypeCustomAttributes;

        public override ParameterInfo ReturnParameter => _inner.ReturnParameter;

        public override bool IsSecurityCritical => _inner.IsSecurityCritical;

        public override bool IsSecuritySafeCritical => _inner.IsSecuritySafeCritical;

        public override bool IsSecurityTransparent => _inner.IsSecurityTransparent;

        public override bool ContainsGenericParameters => _inner.ContainsGenericParameters;

        public override bool IsGenericMethod => _inner.IsGenericMethod;

        public override bool IsGenericMethodDefinition => _inner.IsGenericMethodDefinition;

        public override MethodInfo GetBaseDefinition() => WrapIfNeeded(_inner.GetBaseDefinition());

        public override Type[] GetGenericArguments() => _inner.GetGenericArguments();

        public override MethodInfo GetGenericMethodDefinition() => WrapIfNeeded(_inner.GetGenericMethodDefinition());

        public override MethodInfo MakeGenericMethod(params Type[] typeArguments)
            => WrapIfNeeded(_inner.MakeGenericMethod(typeArguments));

        public override ParameterInfo[] GetParameters() => _inner.GetParameters();

        public override MethodImplAttributes GetMethodImplementationFlags() => _inner.GetMethodImplementationFlags();

        public override MethodBody? GetMethodBody() => _inner.GetMethodBody();

        public override object Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
            => _inner.Invoke(obj, invokeAttr, binder, parameters, culture);

        public override object[] GetCustomAttributes(bool inherit) => _inner.GetCustomAttributes(inherit);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _inner.GetCustomAttributes(attributeType, inherit);

        public override IList<CustomAttributeData> GetCustomAttributesData() => _inner.GetCustomAttributesData();

        public override bool IsDefined(Type attributeType, bool inherit) => _inner.IsDefined(attributeType, inherit);

        public override string? ToString() => _inner.ToString();

        private MethodInfo WrapIfNeeded(MethodInfo method) => method == _inner ? this : GetInvocationMethod(method);
    }
}
