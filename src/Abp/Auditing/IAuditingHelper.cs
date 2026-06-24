using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Auditing
{
    public interface IAuditingHelper
    {
        bool ShouldSaveAudit(MethodInfo methodInfo, bool defaultValue = false);

        bool ShouldSaveAudit(AbpMethodInfo methodInfo, bool defaultValue = false);

        AuditInfo CreateAuditInfo(Type type, MethodInfo method, object[] arguments);

        AuditInfo CreateAuditInfo(Type type, MethodInfo method, IDictionary<string, object> arguments);

        AuditInfo CreateAuditInfo(Type type, AbpMethodInfo method, object[] arguments);

        void Save(AuditInfo auditInfo);

        Task SaveAsync(AuditInfo auditInfo);
    }
}