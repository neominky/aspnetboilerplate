using Abp.Application.Services;

namespace Abp.Interception.CompileTime.Host.Application;

public interface IBuiltInAspectAppService : IApplicationService
{
    string GetAuditedMessage();

    bool GetUnitOfWorkActive();

    string EchoValidated(ValidatedInputDto input);

    string GetAuthorizedMessage();
}
