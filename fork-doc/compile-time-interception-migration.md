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
    CompileTimeInterceptionConfiguration.Enable();
});
```

Or use `AbpBootstrapper.CreateWithCompileTimeInterception<TModule>()`.

`CompileTimeInterceptionConfiguration.Enable()` disables Castle built-in interceptor registrars (validation, auditing, unit of work, authorization, entity history).

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
| Startup | `services.AddAbp<MyModule>()` | Call `CompileTimeInterceptionConfiguration.Enable()` in the `AddAbp` options delegate (before module `Initialize`) |
| Module class | `public class MyModule` | `public partial class MyModule` |
| Convention registration | `IocManager.RegisterAssemblyByConvention(typeof(MyModule).Assembly)` | `using Abp.Dependency.CompileTime;` then `IocManager.RegisterAssemblyByConvention(typeof(MyModule))` |
| App service resolve | Concrete type + Castle proxy | IoC resolves `{Service}_Intercepted` decorator (generated) |
| Custom interceptors | Often Castle-specific wiring | `AbpInterceptorBase` + `ITransientDependency` + `[AbpInterceptor(typeof(TriggerAttribute))]` (or `[AbpIntercept]`) in the **same assembly** |

Application service classes (`IApplicationService` implementations) usually need **no code changes**. Built-in aspects (`[AbpAuthorize]`, `[Audited]`, `[UnitOfWork]`, `[DisableValidation]`, `[UseCase]`, and related attributes) are analyzed at compile time and baked into `AbpMethodInterceptionMetadata`, registered at startup from generated `{Name}_Intercepted` static constructors via `AbpMethodInterceptionMetadataProvider`.

## Projects and responsibilities

| Project | Role |
|:--------|:-----|
| `Abp.SourceGenerators` | Roslyn source generator. Emits `{Service}_Intercepted`, module IoC partials, and metadata registration. |
| `Abp.SourceGenerators.Runtime` | Runtime support in namespace `Abp.Dependency.CompileTime` (`CompileTimeInterceptionConfiguration`, `AbpInvocationCompileTime`, `AbpInvocationStruct`, `AbpInterceptorBaseAllocationFree`, `IAbpInterceptorSync` / `Task` / `ValueTask` async interfaces, IoC extensions). |
| `Abp` | Shared runtime model: `AbpMethodInfo`, `AbpMethodInterceptionMetadata`, `AbpMethodInterceptionMetadataProvider`, `IAbpInvocation`, framework helpers with metadata fast paths. |
| `Abp.Interception.Castle` | Default Castle DynamicProxy path when compile-time interception is **not** enabled. |

`Abp.SourceGenerators.Runtime` layout mirrors `Abp` folders: `Dependency/CompileTime/`, `Runtime/Validation/Interception/`, and `AbpBootstrapperCompileTimeExtensions.cs` at the project root.

## API and abstraction changes

Castle reflection and compile-time metadata share the same runtime APIs. **Class-bridge custom interceptors use `IAbpInvocation`; allocation-free custom interceptors override `protected Internal*` on stack `AbpInvocationStruct` types.** Framework helpers keep `MethodInfo`-based interfaces and consult baked metadata when present.

| Area | Before | After |
|:-----|:-------|:------|
| Invocation | `Castle.DynamicProxy.IInvocation` | `IAbpInvocation` (`Abp.Dependency`) |
| Method in interceptors | `invocation.Method` | `invocation.MethodInvocationTarget` (may be `AbpMethodInfo` when metadata exists) |
| Async proceed | Castle `Proceed()` | **Class-bridge:** `invocation.CaptureProceedInfo().Invoke()` then await `invocation.ReturnValue` as `Task` / `Task<T>`. **Allocation-free fast path:** `return await invocation.Proceed()` (struct by value). **Allocation-free compat:** `CaptureProceedInfo()` → work → `proceedInfo.Invoke()` (optional; same as `Proceed()` when delegate unchanged). |
| Interceptor base | `AbpInterceptorBase` | Same; class-bridge overrides use `IAbpInvocation`. Allocation-free: `AbpInterceptorBaseAllocationFree` + `protected Internal*` on `AbpInvocationStruct` |
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

## Compile-time execution paths

Generated methods run through stack `AbpInvocationStruct` / `AbpInvocationStruct<TAsync>` and `CompileTimeInvocationInterceptorExecutor`. Built-in interceptors (`AuthorizationInterceptor`, `AuditingInterceptor`, …) inherit `AbpInterceptorBase` only and always use the **class-bridge** path (`AbpInvocationCompileTime` implementing `IAbpInvocation`).

User interceptors are routed per method layer:

| Return shape | Class-bridge (`AbpInterceptorBase`) | Allocation-free (`AbpInterceptorBaseAllocationFree`) |
|:-------------|:------------------------------------|:-----------------------------------------------------|
| sync | `RunSyncLayer` → `InterceptSynchronous(IAbpInvocation)` | `RunSyncAllocationFreeLayer` → `IAbpInterceptorSync` → `protected InternalInterceptSynchronous(ref AbpInvocationStruct)` |
| `Task` / `Task<T>` | `RunTaskViaClassBridgeLayer` → `InternalInterceptAsynchronous(IAbpInvocation)` | `RunTaskAllocationFreeLayer` → `IAbpInterceptorTaskAsync` → `protected InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>>)` (by value) |
| `ValueTask` / `ValueTask<T>` | `RunValueTaskViaClassBridgeLayer` (Task-compatible bridge) | `RunValueTaskAllocationFreeLayer`, or `RunValueTaskViaTaskStructLayer` when only Task struct `Internal*` is overridden |

`AbpInvocationCompileTimeAsyncBridge` connects struct layers to class-bridge interceptors when built-in and user interceptors are mixed in one chain. For allocation-free interceptors it calls `IAbpInterceptorSync` / `IAbpInterceptorTaskAsync` / `IAbpInterceptorValueTaskAsync` directly (no `IAbpInvocation` on the compile-time path).

The generator picks allocation-free routing when `AllocationFreeInterceptorAnalyzer` finds non–proceed-only overrides of `protected Internal*` struct methods on `AbpInterceptorBaseAllocationFree`.

## 5. User-defined interceptors

**Class-bridge** (recommended default; same pattern as built-in interceptors). See `test/Abp.Interception.CompileTime.Host/Interceptors/TaggedCompileTimeInterceptor.cs`:

```csharp
[AbpInterceptor(typeof(TaggedAttribute))]
public sealed class TaggedCompileTimeInterceptor : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation) { /* ... */ }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation) { /* ... */ }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation) { /* ... */ }
}
```

`ValueTask` returning application methods still enter the same `InternalInterceptAsynchronous` / `InternalInterceptAsynchronous<TResult>` overrides; the executor materializes `ValueTask` to `Task` at the bridge when needed.

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
| `TaggedCompileTimeInterceptorWebTests` | User interceptor via class-bridge (`AbpInterceptorBase` + `IAbpInvocation`) |
| `StructAllocationFreeInterceptorWebTests` | Allocation-free struct path: `StructFastPathCompileTimeInterceptor` (fast `Proceed`) and `StructCompatCompileTimeInterceptor` (`CaptureProceedInfo` compat) |
| `BuiltInInterceptorWebTests` | Built-in auditing with user interceptors in the same chain |

With `EmitCompilerGeneratedFiles` on the host project, generated sources appear under `obj/Generated/Abp.SourceGenerators/`.
