namespace Abp.Dependency
{
    /// <summary>
    /// Captured proceed information for async interceptor chains (Castle-compatible shape).
    /// </summary>
    public interface IAbpProceedInfo
    {
        void Invoke();
    }
}
