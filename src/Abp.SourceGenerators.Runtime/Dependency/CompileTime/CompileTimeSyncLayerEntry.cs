using System;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// One sync interceptor layer baked at compile time. Stored in a cached array;
    /// use <see cref="CompileTimeSyncLayerList.AsSpan"/> for stack-friendly iteration.
    /// Exactly one of <see cref="ClassInterceptor"/> or <see cref="AllocationFree"/> is set.
    /// </summary>
    public readonly struct CompileTimeSyncLayerEntry
    {
        public CompileTimeSyncLayerEntry(
            AbpInterceptorBase? classInterceptor,
            IAbpInterceptorSync? allocationFree)
        {
            ClassInterceptor = classInterceptor;
            AllocationFree = allocationFree;
        }

        public AbpInterceptorBase? ClassInterceptor { get; }

        public IAbpInterceptorSync? AllocationFree { get; }
    }

    /// <summary>
    /// Cached sync layer table for a generated method (no switch dispatch at runtime).
    /// </summary>
    public sealed class CompileTimeSyncLayerList
    {
        public CompileTimeSyncLayerList(CompileTimeSyncLayerEntry[] layers)
        {
            Layers = layers;
        }

        public CompileTimeSyncLayerEntry[] Layers { get; }

        public ReadOnlySpan<CompileTimeSyncLayerEntry> AsSpan() => Layers;
    }
}
