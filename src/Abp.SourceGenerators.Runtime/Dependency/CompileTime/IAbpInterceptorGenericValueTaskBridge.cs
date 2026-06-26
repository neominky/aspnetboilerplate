using System.Threading.Tasks;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Materializes generic ValueTask return slots for Task-based interceptors without reflection.
    /// </summary>
    internal interface IAbpInterceptorGenericValueTaskBridge
    {
        Task MaterializeReturnAsTask();
    }
}
