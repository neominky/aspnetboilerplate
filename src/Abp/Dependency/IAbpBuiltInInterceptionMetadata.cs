using System.Collections.Generic;
using Abp.Application.Features;
using Abp.Authorization;
using Abp.Domain.Uow;

namespace Abp.Dependency
{
    /// <summary>
    /// Built-in ABP interceptor aspect metadata. Not used by user-defined interceptors.
    /// Populated at compile time for intercepted application services.
    /// </summary>
    public interface IAbpBuiltInInterceptionMetadata : IAbpMethodDescriptor
    {
        UnitOfWorkAttribute? UnitOfWorkAttribute { get; }

        bool ApplyConventionalUnitOfWork { get; }

        bool? ShouldAudit { get; }

        bool ShouldValidate { get; }

        bool AllowAnonymous { get; }

        bool HasUseCaseAttribute { get; }

        string? UseCaseDescription { get; }

        IReadOnlyList<IAbpAuthorizeAttribute>? AuthorizeAttributes { get; }

        IReadOnlyList<RequiresFeatureAttribute>? FeatureAttributes { get; }
    }
}
