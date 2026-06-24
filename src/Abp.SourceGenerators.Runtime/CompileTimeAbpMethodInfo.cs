using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Features;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityHistory;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Compile-time baked <see cref="AbpMethodInfo"/> for built-in ABP interceptors.
    /// </summary>
    public sealed class CompileTimeAbpMethodInfo : AbpMethodInfo, IAbpBuiltInInterceptionMetadata
    {
        private readonly object[] _attributes;
        private readonly Func<object?, object?[]?, object?>? _syncInvoker;
        private readonly Func<object?, object?[]?, Task<object?>>? _asyncInvoker;

        public CompileTimeAbpMethodInfo(
            string name,
            Type declaringType,
            Type returnType,
            bool isPublic,
            IReadOnlyList<AbpParameterInfo> parameters,
            object[] attributes,
            UnitOfWorkAttribute? unitOfWorkAttribute,
            bool applyConventionalUnitOfWork,
            bool? shouldAudit,
            bool shouldValidate,
            bool allowAnonymous,
            bool hasUseCaseAttribute,
            string? useCaseDescription,
            IReadOnlyList<IAbpAuthorizeAttribute>? authorizeAttributes,
            IReadOnlyList<RequiresFeatureAttribute>? featureAttributes,
            Func<object?, object?[]?, object?>? syncInvoker = null,
            Func<object?, object?[]?, Task<object?>>? asyncInvoker = null)
        {
            Name = name;
            DeclaringType = declaringType;
            ReturnType = returnType;
            IsPublic = isPublic;
            Parameters = parameters;
            _attributes = attributes;
            UnitOfWorkAttribute = unitOfWorkAttribute;
            ApplyConventionalUnitOfWork = applyConventionalUnitOfWork;
            ShouldAudit = shouldAudit;
            ShouldValidate = shouldValidate;
            AllowAnonymous = allowAnonymous;
            HasUseCaseAttribute = hasUseCaseAttribute;
            UseCaseDescription = useCaseDescription;
            AuthorizeAttributes = authorizeAttributes;
            FeatureAttributes = featureAttributes;
            _syncInvoker = syncInvoker;
            _asyncInvoker = asyncInvoker;
        }

        public override string Name { get; }

        public override Type DeclaringType { get; }

        public override Type ReturnType { get; }

        public override bool IsPublic { get; }

        public override IReadOnlyList<AbpParameterInfo> Parameters { get; }

        public UnitOfWorkAttribute? UnitOfWorkAttribute { get; }

        public bool ApplyConventionalUnitOfWork { get; }

        public bool? ShouldAudit { get; }

        public bool ShouldValidate { get; }

        public bool AllowAnonymous { get; }

        public bool HasUseCaseAttribute { get; }

        public string? UseCaseDescription { get; }

        public IReadOnlyList<IAbpAuthorizeAttribute>? AuthorizeAttributes { get; }

        public IReadOnlyList<RequiresFeatureAttribute>? FeatureAttributes { get; }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return _attributes.Any(a => attributeType.IsInstanceOfType(a));
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return _attributes;
        }

        protected override Func<object?, object?[]?, object?>? GetSyncInvoker() => _syncInvoker;

        protected override Func<object?, object?[]?, Task<object?>>? GetAsyncInvoker() => _asyncInvoker;
    }
}
