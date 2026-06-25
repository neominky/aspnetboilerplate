using System;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
  /// <summary>
  /// Compile-time IoC registration extensions. Uses the same entry-point name as runtime convention registration.
  /// </summary>
  public static class CompileTimeIocManagerExtensions
  {
    /// <summary>
    /// Registers services for the assembly that contains <paramref name="moduleType"/>.
    /// When compile-time registrations exist on the module partial class, they are used;
    /// otherwise falls back to runtime <see cref="IIocManager.RegisterAssemblyByConvention(System.Reflection.Assembly)"/>.
    /// </summary>
    public static void RegisterAssemblyByConvention(this IIocManager iocManager, Type moduleType)
    {
      Check.NotNull(moduleType, nameof(moduleType));

      EnsureRuntimeServicesRegistered(iocManager);

      if (CompileTimeIocRegistrarRegistry.TryInvoke(moduleType, iocManager))
      {
        return;
      }

      iocManager.RegisterAssemblyByConvention(moduleType.Assembly);
    }

    private static readonly object RuntimeRegistrationSync = new object();
    private static bool _runtimeServicesRegistered;

    private static void EnsureRuntimeServicesRegistered(IIocManager iocManager)
    {
      lock (RuntimeRegistrationSync)
      {
        if (_runtimeServicesRegistered)
        {
          return;
        }

        iocManager.RegisterAssemblyByConvention(typeof(CompileTimeIocManagerExtensions).Assembly);
        _runtimeServicesRegistered = true;
      }
    }
  }
}
