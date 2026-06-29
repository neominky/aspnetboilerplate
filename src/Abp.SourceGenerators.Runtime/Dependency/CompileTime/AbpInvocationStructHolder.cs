namespace Abp.Dependency.CompileTime
{
  /// <summary>
  /// Heap box for <see cref="AbpInvocationStruct"/> in mixed sync chains so class proceed
  /// can advance the same struct instance without <see cref="CompileTimeInvocationScope"/>.
  /// </summary>
  public sealed class AbpInvocationStructHolder
  {
    public AbpInvocationStruct Value;

    public void Reset()
    {
      Value = default;
    }
  }
}
