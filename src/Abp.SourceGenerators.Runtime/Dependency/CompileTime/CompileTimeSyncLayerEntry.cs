using System;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// One sync interceptor layer baked at compile time. Stored in a cached array;
    /// use <see cref="CompileTimeSyncLayerList.AsSpan"/> for stack-friendly iteration.
    /// Exactly one of <see cref="ClassBridge"/> or <see cref="AllocationFree"/> is set.
    /// </summary>
    public readonly struct CompileTimeSyncLayerEntry
    {
        public CompileTimeSyncLayerEntry(
            AbpInterceptorBase? classBridge,
            IAbpInterceptorSync? allocationFree)
        {
            ClassBridge = classBridge;
            AllocationFree = allocationFree;
        }

        public AbpInterceptorBase? ClassBridge { get; }

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
