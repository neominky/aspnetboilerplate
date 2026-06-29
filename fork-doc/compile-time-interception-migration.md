# Compile-Time Interception Migration (Step 1)

This guide describes how to move **service interception** from Castle `DynamicProxy` to the `Abp.SourceGenerators` compile-time path. The Castle and compile-time paths are mutually exclusive for a given application: when compile-time interception is enabled, Castle interceptor registrars are disabled and interception is emitted as C# source.

**Reference sample (NativeAOT):** [`test/Abp.Interception.CompileTime.Host`](../test/Abp.Interception.CompileTime.Host) — compile-time interception host with `<PublishAot>true</PublishAot>`. AOT runtime is not yet fully supported (Castle Windsor); use JIT `dotnet run` for manual checks until roadmap Step 2.

## 1. Add project references

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Abp.SourceGenerators.Runtime/Abp.SourceGenerators.Runtime.csproj" />
  <ProjectReference Include="path/to/Abp.SourceGenerators/Abp.SourceGenerators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Optional (inspect generated code):

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

## 2. Enable compile-time interception at startup (before module `Initialize`)

Castle proxy registration must be turned off **before** `AbpModule.Initialize()` runs. In ASP.NET Core:

```csharp
return services.AddAbp<MyModule>(options =>
{
    options.InterceptorOptions.DisableAuditingInterceptor = true; // optional
    CompileTimeInterceptionConfiguration.Enable(options.InterceptorOptions);
});
```

Or use `AbpBootstrapper.CreateWithCompileTimeInterception<TModule>()`, which forwards `options.InterceptorOptions` after your `configure` delegate runs.

`CompileTimeInterceptionConfiguration.Enable()` disables Castle registrars for validation, auditing, unit of work, authorization, and entity history. Compile-time generated `{Service}_Intercepted` types still **bake** built-in layers from attributes at compile time, but `CompileTimeBuiltInInterceptorProvider` reads startup `InterceptorOptions` at resolve time and substitutes `CompileTimeNoOpInterceptor` for any built-in disabled via `DisableXxxInterceptor` (same effect as skipping Castle registrar registration).

## 3. Use compile-time convention registration in the module

```csharp
using Abp.Dependency.CompileTime;

public partial class MyModule : AbpModule
{
    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(MyModule));
    }
}
```

The source generator emits a `partial` `RegisterAssemblyByConvention` method that registers:

- Eligible services (app services and other intercepted types) → `{Service}_Intercepted` decorator types
- User-defined interceptors (`AbpInterceptorBase` + `ITransientDependency` + `[AbpInterceptor]` or `[AbpIntercept]`)

If no generated registrar exists for the module, the call falls back to runtime `RegisterAssemblyByConvention(assembly)`.

## Migrating from an existing application

Typical ABP apps today use Castle `DynamicProxy` via `Abp.Interception.Castle`: application services are registered by convention, and Windsor attaches `AbpAsyncDeterminationInterceptor<T>` at resolve time. The compile-time path replaces **only** that runtime proxy layer. IoC is still Castle.Windsor until roadmap Step 2.

### Checklist

| Step | Before (Castle) | After (compile-time) |
|:-----|:----------------|:---------------------|
| References | `Abp` / `Abp.AspNetCore` only | Add `Abp.SourceGenerators.Runtime` + `Abp.SourceGenerators` analyzer |
| Startup | `services.AddAbp<MyModule>()` | Call `CompileTimeInterceptionConfiguration.Enable(options.InterceptorOptions)` in the `AddAbp` options delegate (before module `Initialize`) |
| Module class | `public class MyModule` | `public partial class MyModule` |
| Convention registration | `IocManager.RegisterAssemblyByConvention(typeof(MyModule).Assembly)` | `using Abp.Dependency.CompileTime;` then `IocManager.RegisterAssemblyByConvention(typeof(MyModule))` |
| App service resolve | Concrete type + Castle proxy | IoC resolves `{Service}_Intercepted` decorator (generated) |
| Custom interceptors | Often Castle-specific wiring | `AbpInterceptorBase` + `ITransientDependency` + `[AbpInterceptor(typeof(TriggerAttribute))]` (or `[AbpIntercept]`) in the **same assembly** |

Application service classes (`IApplicationService` implementations) usually need **no code changes**. Built-in aspects (`[AbpAuthorize]`, `[Audited]`, `[UnitOfWork]`, `[DisableValidation]`, `[UseCase]`, and related attributes) are analyzed at compile time and baked into `AbpMethodInterceptionMetadata`, registered at startup from generated `{Name}_Intercepted` static constructors via `AbpMethodInterceptionMetadataProvider`.

## Projects and responsibilities

| Project | Role |
|:--------|:-----|
| `Abp.SourceGenerators` | Roslyn source generator. Emits `{Service}_Intercepted`, module IoC partials, and metadata registration. |
| `Abp.SourceGenerators.Runtime` | Runtime support in namespace `Abp.Dependency.CompileTime` (`CompileTimeInterceptionConfiguration`, `AbpInvocationCompileTime`, `AbpInvocationStruct`, `AbpInterceptorBaseAllocationFree`, `IAbpInterceptorSync` / `Task` / `ValueTask` async interfaces, `CompileTimeInvocationInterceptorExecutor`, `AbpSyncChainStepDelegate` / `AbpAsyncChainStepDelegate`, IoC extensions). |
| `Abp` | Shared runtime model: `AbpMethodInfo`, `AbpMethodInterceptionMetadata`, `AbpMethodInterceptionMetadataProvider`, `IAbpInvocation`, framework helpers with metadata fast paths. |
| `Abp.Interception.Castle` | Default Castle DynamicProxy path when compile-time interception is **not** enabled. |

`Abp.SourceGenerators.Runtime` layout mirrors `Abp` folders: `Dependency/CompileTime/`, `Runtime/Validation/Interception/`, and `AbpBootstrapperCompileTimeExtensions.cs` at the project root.

## API and abstraction changes

Castle reflection and compile-time metadata share the same runtime APIs. **Class invocation custom interceptors use `IAbpInvocation`; allocation-free custom interceptors override `protected Internal*` on stack `AbpInvocationStruct` types.** Framework helpers keep `MethodInfo`-based interfaces and consult baked metadata when present.

| Area | Before | After |
|:-----|:-------|:------|
| Invocation | `Castle.DynamicProxy.IInvocation` | `IAbpInvocation` (`Abp.Dependency`) |
| Method in interceptors | `invocation.Method` | `invocation.MethodInvocationTarget` (may be `AbpMethodInfo` when metadata exists) |
| Async proceed | Castle `Proceed()` | **Class invocation:** `invocation.CaptureProceedInfo().Invoke()` then await `invocation.ReturnValue` as `Task` / `Task<T>`. **Allocation-free fast path:** `return await invocation.Proceed()` (struct by value). **Allocation-free compat:** `CaptureProceedInfo()` → work → `proceedInfo.Invoke()` (optional; same as `Proceed()` when delegate unchanged). |
| Interceptor base | `AbpInterceptorBase` | Same; class invocation overrides use `IAbpInvocation`. Allocation-free: `AbpInterceptorBaseAllocationFree` + `protected Internal*` on `AbpInvocationStruct` |
| Built-in aspect metadata | Read from `MethodInfo` at runtime (reflection) | Compile-time path: baked in `AbpMethodInterceptionMetadata`, looked up via `AbpMethodInfo.TryGetMetadata(method, out metadata)` |
| Helper registration | `ITransientDependency` convention | Same; Castle registrars register interceptor **proxies** only, not helpers |

`AbpInvocationExtensions.GetMethodInvocationTarget` / `GetAbpMethod` return `invocation.MethodInvocationTarget`.

**Framework helpers** (`AuthorizationHelper`, `AuditingHelper`, `MethodInvocationValidator`, `EntityHistoryUseCaseDescriptionProvider`) live in `Abp` and implement the existing `MethodInfo`-based interfaces. Each checks `AbpMethodInfo.TryGetMetadata` first; if no baked metadata exists, behavior falls back to the original reflection path (Castle-compatible).

**Metadata model** (in `Abp.Dependency`):

- `AbpMethodInterceptionMetadata` — baked fields (`ShouldAudit`, `ShouldValidate`, `UnitOfWorkAttribute`, `AuthorizeAttributes`, …)
- `AbpMethodInterceptionMetadataProvider.Instance` — runtime registry populated by generated `{Name}_Intercepted` static constructors
- `AbpMethodInfo` — `MethodInfo` subclass; `AbpMethodInfo.GetInvocationMethod` / `TryGetMetadata` unify Castle and compile-time paths

Generated code resolves method names with `nameof(Type.Method)` so renames are refactor-safe.

## Compile-time attributes (`Abp.Dependency.CompileTime`)

Defined in `Abp.SourceGenerators.Runtime`:

| Attribute | Purpose |
|:----------|:--------|
| `[AbpReflection(Include = true/false)]` | Opt in/out of metadata baking and compile-time interception for a type or method. App services and types with built-in aspects are baked by default. |
| `[AbpInterceptor(typeof(TriggerAttribute))]` | On `AbpInterceptorBase` implementations. Maps a trigger attribute to the interceptor. **Required** for user interceptors; missing trigger causes compile error `ABPCT001`. `AllowMultiple = true`. |
| `[AbpIntercept(typeof(MyInterceptor))]` | On a class or method. Directly wires a user interceptor (alternative to the trigger-attribute pattern). |
| `[DisableConventionalRegistration]` | Excludes a type from compile-time assembly scanning (generated `{Name}_Intercepted` types use this). |

**Convention registration API** — the compile-time extension lives in `Abp.Dependency.CompileTime` and takes the **module type**, not `Assembly`:

```csharp
// Before
IocManager.RegisterAssemblyByConvention(typeof(MyModule).Assembly);

// After
using Abp.Dependency.CompileTime;

public partial class MyModule : AbpModule
{
    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(MyModule));
    }
}
```

If no generated registrar exists for the module, the call falls back to runtime `RegisterAssemblyByConvention(assembly)`.

**Custom interceptors** — migrate from Castle `IInvocation` to `IAbpInvocation`:

```csharp
// Before (Castle-specific)
public override void InterceptSynchronous(IInvocation invocation) { ... }

// After
public override void InterceptSynchronous(IAbpInvocation invocation)
{
    var method = invocation.MethodInvocationTarget;
    var attr = method.GetCustomAttributes<MyAttribute>(inherit: true);
    invocation.Proceed();
}
```

User interceptors are no longer picked up by Windsor interceptor lists. They must inherit `AbpInterceptorBase`, implement `ITransientDependency`, declare at least one `[AbpInterceptor(typeof(TriggerAttribute))]` (or use `[AbpIntercept]` on the target), and live in the same assembly as the scanned services.

### What you can remove or stop doing

- Manual `AbpAsyncDeterminationInterceptor<T>` registration for application services.
- Relying on Castle `IInvocation` in application code.
- Expecting DynamicProxy on `IApplicationService` when compile-time interception is enabled.

### What stays the same

- Module structure, `DependsOn`, `PreInitialize` / `PostInitialize`.
- Application service interfaces and DTOs.
- Built-in interceptor **classes** (`AuthorizationInterceptor`, `UnitOfWorkInterceptor`, etc.) — still resolved from IoC; only the **wiring** changes.
- Castle.Windsor as the IoC container (until Step 2).

## 4. Generated artifacts

For each type that requires compile-time interception, the generator emits:

| Artifact | Role |
|----------|------|
| `{Name}_Intercepted` | IoC-registered decorator (`ITransientDependency`, `[DisableConventionalRegistration]`); static ctor registers `AbpMethodInterceptionMetadata` |
| `{Module}.CompileTime.g.cs` | Module partial with `RegisterAssemblyByConvention(IIocManager)` |

**Eligibility** (any of):

- Implements `IApplicationService` (always compile-time intercepted)
- `[AbpReflection]` on type or method
- Built-in aspect attributes on type or method (`Audited`, `UnitOfWork`, `AbpAuthorize`, `RequiresFeature`, `UseCase`, …)
- `[AbpIntercept]` or user interceptor trigger attributes on type or method

Interceptor order in the generated chain:

```
Validation → Auditing → EntityHistory → UnitOfWork → Authorization → user interceptors → target method
```

Supported method return types: `void`, sync `T`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`.

## Compile-time execution model

The generator picks one of **three paths per method** from the baked interceptor layer kinds (`AllocationFreeInterceptorAnalyzer`). Generated `{Name}_Intercepted` code calls `CompileTimeInvocationInterceptorExecutor` for class invocation layers; allocation-free layers call struct interfaces directly.

**Terminology:** The `IAbpInvocation` / `AbpInvocationCompileTime` path is **class invocation**. It is not a “bridge” anymore — legacy struct↔class bridge helpers (`EnsureClassBridge`, async bridge types) were removed. The counterpart is **struct invocation** (`AbpInvocationStruct`, allocation-free interceptors).

### Path selection

| Path | When | Invocation state | Chain dispatch |
|:-----|:-----|:-----------------|:---------------|
| **Pure class invocation (sync)** | Every layer is `ClassInvocation` | `AbpInvocationCompileTime` from `CompileTimeInvocationPool`, passed as **layer argument**, returned in `finally` | `{Method}_SyncClassLayer0(classInvocation)` |
| **Pure class invocation (async)** | Same | `CompileTimeInvocationScope` (`AsyncLocal`, **nested parent stack**) + pooled `AbpInvocationCompileTime` | `scope.ClassInvocation` + layer methods |
| **Pure allocation-free** | Every layer allocation-free | Stack `AbpInvocationStruct` local | Chain array + `Proceed()` |
| **Mixed (sync)** | Class + allocation-free in one chain | Pooled `AbpInvocationStructHolder` + pooled `AbpInvocationCompileTime`; `inv.ClassInvocation` links struct chain to class layers | Class proceed: `classInvocation.MixedStructHolder!.Value.Proceed()` |
| **Mixed (async)** | Same | `CompileTimeInvocationScope` + `AsyncStructHolder<TAsync>` + pooled class invocation | `asyncHolder.Value.ClassInvocation` for mixed class layers |

**Thread safety (hybrid):**
- **Sync pure class / mixed sync:** no `AsyncLocal`. Local variables + argument threading; mixed sync uses `AbpInvocationStructHolder` so class proceed advances the same struct instance.
- **Async class / mixed async:** `CompileTimeInvocationScope` + `AsyncLocal`. `Begin()`/`Dispose()` **restores the parent scope** for safe reentrancy (nested intercepted calls).
- **Allocation-free:** stack struct (unchanged).

**Object pool:** `CompileTimeInvocationPool` reuses `AbpInvocationCompileTime` and `AbpInvocationStructHolder`. After warmup, sync class invocation is **0 B** allocated (benchmark).

### Baked proceed delegates (no per-call lambda allocation)

Class invocation layers need a `Func<object?>` (sync) or `Func<Task<object?>>` (async) proceed. The generator emits:

| Artifact | Role |
|----------|------|
| `{Method}_SyncClassProceedN` / `{Method}_TaskClassProceedN` / `{Method}_ValueTaskClassProceedN` | Private methods; ctor-wired as `readonly Func<…>` fields |
| **Pure class invocation** proceed | Calls the next `{Method}_SyncClassLayerK` or `{Method}_SyncClassTail` |
| **Mixed** proceed | `classInvocation.MixedStructHolder!.Value.Proceed()` (sync) or async holder `Value.Proceed()` |

Async proceed returns `Task.FromResult<object?>(task)` where `task` is the **`Task<TResult>` object** (not the awaited result). Built-in interceptors read `invocation.ReturnValue` as `Task` / `Task<T>` after `CaptureProceedInfo().Invoke()`.

### Pure class invocation sync (example)

```csharp
// sync entry (pool + argument passing, no AsyncLocal)
var classInvocation = CompileTimeInvocationPool.Rent(_inner, GetMessageMethod, arguments);
try
{
    classInvocation.PrepareForCall(arguments, null);
    GetMessage_SyncClassLayer0(classInvocation);
    return (string)classInvocation.ReturnValue!;
}
finally
{
    CompileTimeInvocationPool.Return(classInvocation);
}

private void GetMessage_SyncClassLayer0(AbpInvocationCompileTime classInvocation)
{
    CompileTimeInvocationInterceptorExecutor.RunSyncClassLayer(
        classInvocation, _auditingInterceptor, _getMessageSyncClassProceed0);
}

private object? GetMessage_SyncClassProceed0(AbpInvocationCompileTime classInvocation)
{
    GetMessage_SyncClassLayer1(classInvocation);
    return classInvocation.ReturnValue;
}
```

### Pure allocation-free sync (example)

```csharp
AbpInvocationStruct syncInvocation = default;
syncInvocation.Initialize(_inner, GetMessageInvocationMethod, arguments);
syncInvocation.BeginSyncChain(_getMessageSyncChain);
syncInvocation.Proceed();
return (string)syncInvocation.ReturnValue!;
```

Chain steps are private methods (`GetMessage_SyncLayer0`, …, `GetMessage_SyncTail`) stored in a `readonly AbpSyncChainStepDelegate[]` assigned in the ctor.

### Mixed sync (example)

Built-in class invocation layers sit at the front of the chain; allocation-free user interceptors follow. Class proceed advances the struct chain:

```csharp
private object? GetMessage_SyncClassProceed0()
{
    _getMessageSyncInvocationSlot.Proceed();
    return _getMessageSyncInvocationSlot.ReturnValue;
}
```

`RunSyncClassLayer(ref inv, interceptor, classInvocation, bakedProceed)` copies arguments into the pre-created `AbpInvocationCompileTime`, runs the interceptor, then merges `ReturnValue` back into the struct.

### Async (Task / ValueTask)

Same three paths. Pure class invocation entry:

```csharp
_getMessageTaskAsyncTaskClassInvocation.PrepareForCall(arguments);
return GetMessageTaskAsync_TaskClassLayer0();
```

`RunTaskClassLayer` / `RunValueTaskClassLayer` (and `ref` overloads for mixed) delegate to `ResolveTaskReturn` / `ResolveValueTaskReturn`. ValueTask proceed uses `.AsTask()` when wrapping `ValueTask<TResult>` into `Task.FromResult<object?>(…)`.

### Interceptor routing (unchanged semantics)

Built-in interceptors (`AuthorizationInterceptor`, `AuditingInterceptor`, …) inherit `AbpInterceptorBase` only and always use the **class invocation** path.

User interceptors are routed per layer:

| Return shape | Class invocation (`AbpInterceptorBase`) | Allocation-free (`AbpInterceptorBaseAllocationFree`) |
|:-------------|:------------------------------------|:-----------------------------------------------------|
| sync | `InterceptSynchronous(IAbpInvocation)` via `AbpInvocationCompileTime` | `IAbpInterceptorSync.InterceptSynchronous(ref AbpInvocationStruct)` |
| `Task` / `Task<T>` | `InterceptAsynchronous` / `InterceptAsynchronous<T>` on `AbpInvocationCompileTime` | `IAbpInterceptorTaskAsync.InterceptAsynchronous<T>(invocation)` |
| `ValueTask` / `ValueTask<T>` | `InterceptAsynchronous` on `AbpInvocationCompileTime` (Task materialization inside executor) | `IAbpInterceptorValueTaskAsync`, or `RunValueTaskTaskStructLayer` when only Task `Internal*` exists |

`AllocationFreeInterceptorAnalyzer` selects allocation-free routing when `AbpInterceptorBaseAllocationFree` overrides non-trivial `protected Internal*` struct methods.

### Caching and reuse

- `AbpMethodInfo.GetInvocationMethod` — `ConcurrentDictionary` cache for invocation `MethodInfo` wrappers.
- Static `AbpInvocationMethod` fields on `{Name}_Intercepted` — one lookup per method at type load.
- `AbpInvocationCompileTime` — **one instance per intercepted method** (sync / Task / ValueTask), reused across calls via `PrepareForCall`.
- Proceed `Func` fields — wired once in ctor via method groups (no per-invocation delegate allocation).

## Benchmarks (fork)

Projects under [`benchmark/`](../benchmark/):

| Project | Role |
|---------|------|
| `Abp.Interception.Benchmarks.Contracts` | Shared counters and verification |
| `Abp.Interception.Benchmarks.NuGet` | NuGet Abp 10.4 + Castle DynamicProxy |
| `Abp.Interception.Benchmarks.Fork` | This fork + `Abp.SourceGenerators` (class invocation vs allocation-free) |
| `Abp.Interception.Benchmarks.RunAll` | Runs NuGet and Fork in **separate processes** (same `Abp` assembly cannot load twice) |

Run (Release):

```bash
dotnet run -c Release --project benchmark/Abp.Interception.Benchmarks.NuGet -- --filter "*"
dotnet run -c Release --project benchmark/Abp.Interception.Benchmarks.Fork -- --filter "*"
dotnet run -c Release --project benchmark/Abp.Interception.Benchmarks.Fork -- --filter "*" --verify
```

Each scenario: **3 user interceptors**, 1 sync or async call per iteration. Sample results (.NET 9, i9-14900KF, Release, measured after thread-safe `CompileTimeInvocationScope`):

| Scenario | NuGet Castle | Fork class invocation | Fork allocation-free |
|----------|-------------:|------------------------:|---------------------:|
| Sync | 40.9 ns / 104 B | **~69 ns / 0 B** | **~15 ns / 0 B** |
| Task async | 962.5 ns / 806 B | ~1,039 ns / 979 B | ~911 ns / 749 B |

Full Fork BenchmarkDotNet output (same machine, after pool warmup):

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Compile-time (class invocation) | 68.95 ns | **0 B** |
| Compile-time (allocation-free) | 15.13 ns | 0 B |
| Compile-time (class invocation) Task | 1,039.12 ns | 979 B |
| Compile-time (allocation-free) Task | 911.01 ns | 749 B |

Sync class invocation is **0 B** thanks to pooling and thread-safe via per-call rent/return. Slightly slower than Castle on sync latency. Async class invocation pays `CompileTimeInvocationScope` cost (~1 KB/call). Allocation-free wins on both sync and async vs Castle.

## Compile-time execution paths (`CompileTimeInvocationInterceptorExecutor`)

Generated `{Name}_Intercepted` calls `CompileTimeInvocationInterceptorExecutor` for every class invocation layer. Allocation-free layers call struct interceptor interfaces directly.

| Return shape | Pure class invocation | Mixed class invocation | Allocation-free |
|:-------------|:------------------|:-------------------|:----------------|
| sync | `RunSyncClassLayer(invocation, interceptor, proceed)` | `RunSyncClassLayer(ref inv, interceptor, invocation, proceed)` | `RunSyncAllocationFreeLayer(ref inv, interceptor)` |
| `Task` / `Task<T>` | `RunTaskClassLayer<T>(invocation, interceptor, proceed)` | `RunTaskClassLayer<T>(ref inv, interceptor, invocation, proceed)` | `RunTaskAllocationFreeLayer<T>(ref inv, interceptor)` |
| `ValueTask` / `ValueTask<T>` | `RunValueTaskClassLayer<T>(invocation, interceptor, proceed)` | `RunValueTaskClassLayer<T>(ref inv, interceptor, invocation, proceed)` | `RunValueTaskAllocationFreeLayer<T>` / `RunValueTaskTaskStructLayer<T>` |

`ResolveTaskReturn` / `ResolveValueTaskReturn` normalize `AbpInvocationCompileTime.ReturnValue` after the interceptor runs.

**Removed** (no longer emitted or referenced): `EnsureClassBridge`, `SyncInvocationState`, `AbpInvocationCompileTimeAsyncBridge`, `AbpInvocationCompileTimeTaskCompatible`, nested `RunTaskViaClassBridgeLayer` lambdas.

Built-in interceptors always use class invocation. User interceptors: see routing table in **Compile-time execution model** above.

## 5. User-defined interceptors

**Class invocation** (recommended default; same pattern as built-in interceptors). See `test/Abp.Interception.CompileTime.Host/Interceptors/TaggedCompileTimeInterceptor.cs`:

```csharp
[AbpInterceptor(typeof(TaggedAttribute))]
public sealed class TaggedCompileTimeInterceptor : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation) { /* ... */ }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation) { /* ... */ }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation) { /* ... */ }
}
```

`ValueTask` returning application methods still enter the same `InternalInterceptAsynchronous` / `InternalInterceptAsynchronous<TResult>` overrides; `CompileTimeInvocationInterceptorExecutor` materializes `ValueTask` to `Task` at the class invocation layer when needed.

**Allocation-free** (optional; fewer heap allocations on the user interceptor layer). Inherit `AbpInterceptorBaseAllocationFree` and override `protected Internal*` struct methods only — do **not** override public `InterceptSynchronous` / `InterceptAsynchronous` on the struct interfaces. See:

- **Fast path (preferred):** [`StructFastPathCompileTimeInterceptor.cs`](../test/Abp.Interception.CompileTime.Host/Interceptors/StructFastPathCompileTimeInterceptor.cs) — sync `ref` + `Proceed()`, async `return await invocation.Proceed()`.
- **Built-in porting (compatibility):** [`StructCompatCompileTimeInterceptor.cs`](../test/Abp.Interception.CompileTime.Host/Interceptors/StructCompatCompileTimeInterceptor.cs) — async `CaptureProceedInfo()` before work, then `proceedInfo.Invoke()` (same shape as `IAbpInvocation` interceptors).

```csharp
// Fast path
[AbpInterceptor(typeof(StructFastPathTaggedAttribute))]
public sealed class StructFastPathCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
    {
        /* pre-work */ invocation.Proceed();
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(
        AbpInvocationStruct<Task<TResult>> invocation)
    {
        /* pre-work */ return await invocation.Proceed().ConfigureAwait(false);
    }

    protected override async ValueTask<TResult> InternalInterceptAsynchronous<TResult>(
        AbpInvocationStruct<ValueTask<TResult>> invocation)
    {
        /* pre-work */ return await invocation.Proceed().ConfigureAwait(false);
    }
}

// IAbpInvocation compatibility (porting existing async interceptors)
[AbpInterceptor(typeof(StructCompatTaggedAttribute))]
public sealed class StructCompatCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(
        AbpInvocationStruct<Task<TResult>> invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();
        /* pre-work (may await) */
        return await proceedInfo.Invoke().ConfigureAwait(false);
    }
}
```

Sync uses `ref AbpInvocationStruct` so `ReturnValue` is updated in place. Async struct parameters are **by value** (shared proceed delegate; supports `async override`). Void `Task` / `ValueTask` methods use `AbpUnit` as the async result type internally (`AbpAsyncCoercion` in the emitter).

`CaptureProceedInfo()` on `AbpInvocationStruct<TAsync>` is optional — functionally equivalent to `Proceed()` when the proceed delegate is unchanged; keep it when translating `IAbpInvocation` interceptors line-for-line.

`AbpInterceptorBaseAllocationFree` also implements Castle / legacy `IAbpInvocation` entry points by adapting into stack structs and forwarding to the same `protected Internal*` methods.

Trigger attribute pattern:

```csharp
[Tagged("demo")]
public class MyAppService : ApplicationService { /* ... */ }
```

Direct wiring with `[AbpIntercept(typeof(MyInterceptor))]` on a class or method is also supported.

- **App services**: all discovered user interceptors run in the chain.
- **Non–app services**: only interceptors whose trigger or `[AbpIntercept]` matches the type or method are applied.

The generator discovers interceptors at compile time, registers them in IoC, and bakes them into the chain. No runtime assembly scanning is used.

## 6. NativeAOT publishing (optional)

For full AOT publish, see the sample project:

- `<PublishAot>true</PublishAot>`
- `ILLink.Descriptors.xml` for trimmer roots
- `EmitCompilerGeneratedFiles` to inspect output under `obj/Generated/Abp.SourceGenerators/`

## 7. What stays on the Castle path (for now)

`Abp.Web.Common` references `Abp.Interception.Castle` for the default stack. Compile-time interception replaces **runtime DynamicProxy for registered services** only when explicitly enabled via `CompileTimeInterceptionConfiguration.Enable()`. IoC remains Castle.Windsor until roadmap Step 2.

## 8. Verify

Run [`test/Abp.Interception.CompileTime.Tests`](../test/Abp.Interception.CompileTime.Tests) against [`test/Abp.Interception.CompileTime.Host`](../test/Abp.Interception.CompileTime.Host):

| Test class | What it checks |
|:-----------|:---------------|
| `TaggedCompileTimeInterceptorWebTests` | User interceptor via class invocation (`AbpInterceptorBase` + `IAbpInvocation`) |
| `StructAllocationFreeInterceptorWebTests` | Allocation-free struct path: `StructFastPathCompileTimeInterceptor` (fast `Proceed`) and `StructCompatCompileTimeInterceptor` (`CaptureProceedInfo` compat) |
| `BuiltInInterceptorWebTests` | Built-in auditing with user interceptors in the same chain |

With `EmitCompilerGeneratedFiles` on the host project, generated sources appear under `obj/Generated/Abp.SourceGenerators/`.

Benchmark verification (3 interceptors per call):

```bash
dotnet run -c Release --project benchmark/Abp.Interception.Benchmarks.Fork -- --filter "*" --verify
```
