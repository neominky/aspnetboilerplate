using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Abp.Dependency.CompileTime
{
  /// <summary>
  /// Lightweight pools for compile-time interception hot paths. Avoids per-call allocation
  /// after warmup while keeping invocation state off intercepted service instances.
  /// </summary>
  public static class CompileTimeInvocationPool
  {
    private static readonly ConcurrentBag<AbpInvocationCompileTime> SyncInvocations = new();
    private static readonly ConcurrentBag<AbpInvocationStructHolder> StructHolders = new();

    public static AbpInvocationCompileTime Rent(
        object invocationTarget,
        MethodInfo method,
        object?[] arguments,
        bool wrapMethodWithMetadata = true)
    {
      if (SyncInvocations.TryTake(out var invocation))
      {
        invocation.Reinitialize(invocationTarget, method, arguments, wrapMethodWithMetadata);
        return invocation;
      }

      return new AbpInvocationCompileTime(invocationTarget, method, arguments, wrapMethodWithMetadata);
    }

    public static AbpInvocationCompileTime<TResult> Rent<TResult>(
        object invocationTarget,
        MethodInfo method,
        object?[] arguments,
        bool wrapMethodWithMetadata = false)
    {
      if (SyncInvocations.TryTake(out var invocation) && invocation is AbpInvocationCompileTime<TResult> typedInvocation)
      {
        typedInvocation.Reinitialize(invocationTarget, method, arguments, wrapMethodWithMetadata);
        return typedInvocation;
      }

      return new AbpInvocationCompileTime<TResult>(invocationTarget, method, arguments, wrapMethodWithMetadata);
    }

    public static void Return(AbpInvocationCompileTime invocation)
    {
      invocation.ResetForPool();
      SyncInvocations.Add(invocation);
    }

    public static AbpInvocationStructHolder RentStructHolder()
    {
      if (StructHolders.TryTake(out var holder))
      {
        holder.Reset();
        return holder;
      }

      return new AbpInvocationStructHolder();
    }

    public static void Return(AbpInvocationStructHolder holder)
    {
      holder.Reset();
      StructHolders.Add(holder);
    }
  }
}
