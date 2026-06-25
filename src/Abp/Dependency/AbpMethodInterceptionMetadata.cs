using System.Collections.Generic;
using Abp.Application.Features;
using Abp.Authorization;
using Abp.Domain.Uow;

namespace Abp.Dependency
{
    /// <summary>
    /// Baked built-in interceptor aspect metadata for a method.
    /// </summary>
    public sealed class AbpMethodInterceptionMetadata
    {
        public UnitOfWorkAttribute? UnitOfWorkAttribute { get; init; }

        public bool ApplyConventionalUnitOfWork { get; init; }

        public bool? ShouldAudit { get; init; }

        public bool ShouldValidate { get; init; }

        public bool AllowAnonymous { get; init; }

        public bool HasUseCaseAttribute { get; init; }

        public string? UseCaseDescription { get; init; }

        public IReadOnlyList<IAbpAuthorizeAttribute>? AuthorizeAttributes { get; init; }

        public IReadOnlyList<RequiresFeatureAttribute>? FeatureAttributes { get; init; }
    }
}
