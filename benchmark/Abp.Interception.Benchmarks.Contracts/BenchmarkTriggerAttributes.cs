namespace Abp.Interception.Benchmarks.Contracts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchmarkTrigger1Attribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchmarkTrigger2Attribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchmarkTrigger3Attribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchmarkAllocationFreeTrigger1Attribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchmarkAllocationFreeTrigger2Attribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchmarkAllocationFreeTrigger3Attribute : Attribute;
