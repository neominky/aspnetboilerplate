using System.Collections.Generic;
using Abp.Application.Features;
using Abp.Authorization;
using Abp.Domain.Uow;

namespace Abp.Dependency.CompileTime
{
  public readonly struct CompileTimeMethodAspectOptions
  {
    public string ServiceName { get; init; }
    public string MethodName { get; init; }
    public UnitOfWorkOptions? UnitOfWork { get; init; }
    public bool Audit { get; init; }
    public bool Validate { get; init; }
    public bool AllowAnonymous { get; init; }
    public AbpAuthorizeAttribute[]? AuthorizeAttributes { get; init; }
    public RequiresFeatureAttribute[]? FeatureAttributes { get; init; }
    public Dictionary<string, object?>? AuditParameters { get; init; }
  }
}
