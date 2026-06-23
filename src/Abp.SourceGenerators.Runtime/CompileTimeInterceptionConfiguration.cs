namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Global switch for compile-time IoC registration and interception.
    /// When enabled, Castle DynamicProxy interceptors are not registered.
    /// </summary>
    public static class CompileTimeInterceptionConfiguration
    {
        public static bool IsEnabled { get; private set; }

        public static void Enable()
        {
            IsEnabled = true;
        }

        public static void Disable()
        {
            IsEnabled = false;
        }
    }
}
