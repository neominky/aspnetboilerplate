using Abp.Application.Services;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.Runtime.Validation;

namespace Abp.Interception.CompileTime.Host.Application;

public class BuiltInAspectAppService : ApplicationService, IBuiltInAspectAppService
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public BuiltInAspectAppService(IUnitOfWorkManager unitOfWorkManager)
    {
        _unitOfWorkManager = unitOfWorkManager;
    }

    [Audited]
    [DisableValidation]
    public string GetAuditedMessage()
    {
        return "audited";
    }

    [DisableValidation]
    public bool GetUnitOfWorkActive()
    {
        return _unitOfWorkManager.Current != null;
    }

    public string EchoValidated(ValidatedInputDto input)
    {
        return input.Name;
    }

    [AbpAuthorize("Sample.Permission")]
    [DisableValidation]
    public string GetAuthorizedMessage()
    {
        return "authorized";
    }
}
