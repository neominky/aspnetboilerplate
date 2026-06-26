using System.Collections.Concurrent;
using System.Threading.Tasks;
using Abp.Auditing;

namespace Abp.Interception.CompileTime.Host.Infrastructure;

public sealed class TestAuditingStore : IAuditingStore
{
    private readonly ConcurrentBag<AuditInfo> _logs = new();

    public static AuditInfo? LastAudit { get; private set; }

    public static void ResetForTest()
    {
        LastAudit = null;
    }

    public void Save(AuditInfo auditInfo)
    {
        _logs.Add(auditInfo);
        LastAudit = auditInfo;
    }

    public Task SaveAsync(AuditInfo auditInfo)
    {
        Save(auditInfo);
        return Task.CompletedTask;
    }
}
