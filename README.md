# ASP.NET Boilerplate

[![Build Status](https://github.com/aspnetboilerplate/aspnetboilerplate/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/aspnetboilerplate/aspnetboilerplate/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/v/Abp.svg?style=flat-square)](https://www.nuget.org/packages/Abp)
[![MyGet (with prereleases)](https://img.shields.io/myget/abp-nightly/vpre/Abp.svg?style=flat-square)](https://aspnetboilerplate.com/Pages/Documents/Nightly-Builds)
[![NuGet Download](https://img.shields.io/nuget/dt/Abp.svg?style=flat-square)](https://www.nuget.org/packages/Abp)

## Fork Purpose

This repository is a fork of [ASP.NET Boilerplate](https://github.com/aspnetboilerplate/aspnetboilerplate). It is maintained and evolved with goals that differ from the upstream project.

### Goals

1. **NativeAOT** — Restructure the framework for compatibility with ahead-of-time (AOT) compilation. The focus is on removing or replacing anything that relies on runtime code generation or dynamic assembly loading with compile-time alternatives.
2. **Modernization** — Bring .NET, dependency packages, and build/deploy practices up to current standards.
3. **Remove and replace AutoMapper** — Drop the `Abp.AutoMapper` integration and AutoMapper itself. Object-to-object mapping will move to compile-time alternatives (e.g. source generators) so mapping stays AOT-friendly and free of runtime reflection-based configuration.

### Migration Roadmap

To reach these goals, **Castle.Windsor** is removed first, then **AutoMapper**. ABP depends heavily on the Castle stack for IoC and interception (auditing, validation, unit of work, and more), and on AutoMapper for DTO mapping across application services. Both rely on runtime reflection and dynamic configuration, which are major barriers to NativeAOT and alignment with the modern .NET ecosystem.

| Step | Task | Description |
|:-----|:-----|:------------|
| 1 | **DynamicProxy → Source Generator** | Replace `Castle.DynamicProxy` runtime proxies with source generators. Interceptor and proxy logic is emitted at compile time so it works with AOT trimming. |
| 2 | **Replace IoC → Remove Castle.Windsor** | Migrate IoC to a standard DI container such as `Microsoft.Extensions.DependencyInjection`, then fully remove Castle.Windsor and related packages. |
| 3 | **AutoMapper → Source Generator** | Replace `AutoMapper` / `Abp.AutoMapper` with compile-time mapping (source generators). Remove runtime profile scanning and expression-tree mapping in favor of generated `Map` methods. |

```
Remove Castle.Windsor
    ├── Step 1: DynamicProxy → Source Generator
    └── Step 2: Replace IoC (MS.DI, etc.)
            └── NativeAOT · Modernization
Remove AutoMapper
    └── Step 3: AutoMapper → Source Generator
            └── NativeAOT · Modernization
```

### Compile-Time Interception Migration (Step 1)

This section describes how to move **service interception** from Castle `DynamicProxy` to the `Abp.SourceGenerators` compile-time path. The Castle and compile-time paths are mutually exclusive for a given application: when compile-time interception is enabled, Castle interceptor registrars are disabled and interception is emitted as C# source.

**Reference sample (NativeAOT):** [`test/Abp.Interception.CompileTime.Host`](test/Abp.Interception.CompileTime.Host) — compile-time interception host with `<PublishAot>true</PublishAot>`. AOT runtime is not yet fully supported (Castle Windsor); use JIT `dotnet run` for manual checks until roadmap Step 2.

#### 1. Add project references

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

#### 2. Enable compile-time interception at startup (before module `Initialize`)

Castle proxy registration must be turned off **before** `AbpModule.Initialize()` runs. In ASP.NET Core:

```csharp
return services.AddAbp<MyModule>(options =>
{
    CompileTimeInterceptionConfiguration.Enable();
});
```

Or use `AbpBootstrapper.CreateWithCompileTimeInterception<TModule>()`.

`CompileTimeInterceptionConfiguration.Enable()` disables Castle built-in interceptor registrars (validation, auditing, unit of work, authorization, entity history).

#### 3. Use compile-time convention registration in the module

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

#### Migrating from an existing application

Typical ABP apps today use Castle `DynamicProxy` via `Abp.Interception.Castle`: application services are registered by convention, and Windsor attaches `AbpAsyncDeterminationInterceptor<T>` at resolve time. The compile-time path replaces **only** that runtime proxy layer. IoC is still Castle.Windsor until roadmap Step 2.

**Checklist**

| Step | Before (Castle) | After (compile-time) |
|:-----|:----------------|:---------------------|
| References | `Abp` / `Abp.AspNetCore` only | Add `Abp.SourceGenerators.Runtime` + `Abp.SourceGenerators` analyzer |
| Startup | `services.AddAbp<MyModule>()` | Call `CompileTimeInterceptionConfiguration.Enable()` in the `AddAbp` options delegate (before module `Initialize`) |
| Module class | `public class MyModule` | `public partial class MyModule` |
| Convention registration | `IocManager.RegisterAssemblyByConvention(typeof(MyModule).Assembly)` | `using Abp.Dependency.CompileTime;` then `IocManager.RegisterAssemblyByConvention(typeof(MyModule))` |
| App service resolve | Concrete type + Castle proxy | IoC resolves `{Service}_Intercepted` decorator (generated) |
| Custom interceptors | Often Castle-specific wiring | `AbpInterceptorBase` + `ITransientDependency` + `[AbpInterceptor(typeof(TriggerAttribute))]` (or `[AbpIntercept]`) in the **same assembly** |

Application service classes (`IApplicationService` implementations) usually need **no code changes**. Built-in aspects (`[AbpAuthorize]`, `[Audited]`, `[UnitOfWork]`, `[DisableValidation]`, `[UseCase]`, and related attributes) are analyzed at compile time and baked into `AbpMethodInterceptionMetadata`, registered at startup from generated `{Name}_Intercepted` static constructors via `AbpMethodInterceptionMetadataProvider`.

#### Projects and responsibilities

| Project | Role |
|:--------|:-----|
| `Abp.SourceGenerators` | Roslyn source generator. Emits `{Service}_Intercepted`, module IoC partials, and metadata registration. |
| `Abp.SourceGenerators.Runtime` | Runtime support in namespace `Abp.Dependency.CompileTime` (`CompileTimeInterceptionConfiguration`, `AbpInvocationCompileTime`, `AbpInvocationStruct`, `AbpInterceptorBaseAllocationFree`, `IAbpInterceptorSync` / `Task` / `ValueTask` async interfaces, IoC extensions). |
| `Abp` | Shared runtime model: `AbpMethodInfo`, `AbpMethodInterceptionMetadata`, `AbpMethodInterceptionMetadataProvider`, `IAbpInvocation`, framework helpers with metadata fast paths. |
| `Abp.Interception.Castle` | Default Castle DynamicProxy path when compile-time interception is **not** enabled. |

`Abp.SourceGenerators.Runtime` layout mirrors `Abp` folders: `Dependency/CompileTime/`, `Runtime/Validation/Interception/`, and `AbpBootstrapperCompileTimeExtensions.cs` at the project root.

#### API and abstraction changes

Castle reflection and compile-time metadata share the same runtime APIs. **Class-bridge custom interceptors use `IAbpInvocation`; allocation-free custom interceptors override `protected Internal*` on stack `AbpInvocationStruct` types.** Framework helpers keep `MethodInfo`-based interfaces and consult baked metadata when present.

| Area | Before | After |
|:-----|:-------|:------|
| Invocation | `Castle.DynamicProxy.IInvocation` | `IAbpInvocation` (`Abp.Dependency`) |
| Method in interceptors | `invocation.Method` | `invocation.MethodInvocationTarget` (may be `AbpMethodInfo` when metadata exists) |
| Async proceed | Castle `Proceed()` | `invocation.CaptureProceedInfo().Invoke()` then await `invocation.ReturnValue` as `Task` / `Task<T>` |
| Interceptor base | `AbpInterceptorBase` | Same; overrides use `IAbpInvocation` |
| Built-in aspect metadata | Read from `MethodInfo` at runtime (reflection) | Compile-time path: baked in `AbpMethodInterceptionMetadata`, looked up via `AbpMethodInfo.TryGetMetadata(method, out metadata)` |
| Helper registration | `ITransientDependency` convention | Same; Castle registrars register interceptor **proxies** only, not helpers |

`AbpInvocationExtensions.GetMethodInvocationTarget` / `GetAbpMethod` return `invocation.MethodInvocationTarget`.

**Framework helpers** (`AuthorizationHelper`, `AuditingHelper`, `MethodInvocationValidator`, `EntityHistoryUseCaseDescriptionProvider`) live in `Abp` and implement the existing `MethodInfo`-based interfaces. Each checks `AbpMethodInfo.TryGetMetadata` first; if no baked metadata exists, behavior falls back to the original reflection path (Castle-compatible).

**Metadata model** (in `Abp.Dependency`):

- `AbpMethodInterceptionMetadata` — baked fields (`ShouldAudit`, `ShouldValidate`, `UnitOfWorkAttribute`, `AuthorizeAttributes`, …)
- `AbpMethodInterceptionMetadataProvider.Instance` — runtime registry populated by generated `{Name}_Intercepted` static constructors
- `AbpMethodInfo` — `MethodInfo` subclass; `AbpMethodInfo.GetInvocationMethod` / `TryGetMetadata` unify Castle and compile-time paths

Generated code resolves method names with `nameof(Type.Method)` so renames are refactor-safe.

#### Compile-time attributes (`Abp.Dependency.CompileTime`)

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

**What you can remove or stop doing**

- Manual `AbpAsyncDeterminationInterceptor<T>` registration for application services.
- Relying on Castle `IInvocation` in application code.
- Expecting DynamicProxy on `IApplicationService` when compile-time interception is enabled.

**What stays the same**

- Module structure, `DependsOn`, `PreInitialize` / `PostInitialize`.
- Application service interfaces and DTOs.
- Built-in interceptor **classes** (`AuthorizationInterceptor`, `UnitOfWorkInterceptor`, etc.) — still resolved from IoC; only the **wiring** changes.
- Castle.Windsor as the IoC container (until Step 2).

#### 4. Generated artifacts

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

#### Compile-time execution paths

Generated methods run through stack `AbpInvocationStruct` / `AbpInvocationStruct<TAsync>` and `CompileTimeInvocationInterceptorExecutor`. Built-in interceptors (`AuthorizationInterceptor`, `AuditingInterceptor`, …) inherit `AbpInterceptorBase` only and always use the **class-bridge** path (`AbpInvocationCompileTime` implementing `IAbpInvocation`).

User interceptors are routed per method layer:

| Return shape | Class-bridge (`AbpInterceptorBase`) | Allocation-free (`AbpInterceptorBaseAllocationFree`) |
|:-------------|:------------------------------------|:-----------------------------------------------------|
| sync | `RunSyncLayer` → `InterceptSynchronous(IAbpInvocation)` | `RunSyncAllocationFreeLayer` → `IAbpInterceptorSync` → `protected InternalInterceptSynchronous(ref AbpInvocationStruct)` |
| `Task` / `Task<T>` | `RunTaskViaClassBridgeLayer` → `InternalInterceptAsynchronous(IAbpInvocation)` | `RunTaskAllocationFreeLayer` → `IAbpInterceptorTaskAsync` → `protected InternalInterceptAsynchronous(ref AbpInvocationStruct<Task[...]>)` |
| `ValueTask` / `ValueTask<T>` | `RunValueTaskViaClassBridgeLayer` (Task-compatible bridge) | `RunValueTaskAllocationFreeLayer`, or `RunValueTaskViaTaskStructLayer` when only Task struct `Internal*` is overridden |

`AbpInvocationCompileTimeAsyncBridge` connects struct layers to class-bridge interceptors when built-in and user interceptors are mixed in one chain. For allocation-free interceptors it calls `IAbpInterceptorSync` / `IAbpInterceptorTaskAsync` / `IAbpInterceptorValueTaskAsync` directly (no `IAbpInvocation` on the compile-time path).

The generator picks allocation-free routing when `AllocationFreeInterceptorAnalyzer` finds non–proceed-only overrides of `protected Internal*` struct methods on `AbpInterceptorBaseAllocationFree`.

#### 5. User-defined interceptors

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

**Allocation-free** (optional; fewer heap allocations on the user interceptor layer). Inherit `AbpInterceptorBaseAllocationFree` and override `protected Internal*` struct methods only — do **not** override public `InterceptSynchronous(ref …)` / `InterceptAsynchronous(ref …)`. See `test/Abp.Interception.CompileTime.Host/Interceptors/StructTaggedCompileTimeInterceptor.cs`:

```csharp
[AbpInterceptor(typeof(StructTaggedAttribute))]
public sealed class StructTaggedCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation) { /* ... */ }

    protected override Task InternalInterceptAsynchronous(ref AbpInvocationStruct<Task> invocation) { /* ... */ }

    protected override Task<TResult> InternalInterceptAsynchronous<TResult>(ref AbpInvocationStruct<Task<TResult>> invocation) { /* ... */ }

    protected override ValueTask InternalInterceptAsynchronous(ref AbpInvocationStruct<ValueTask> invocation) { /* ... */ }

    protected override ValueTask<TResult> InternalInterceptAsynchronous<TResult>(ref AbpInvocationStruct<ValueTask<TResult>> invocation) { /* ... */ }
}
```

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

#### 6. NativeAOT publishing (optional)

For full AOT publish, see the sample project:

- `<PublishAot>true</PublishAot>`
- `ILLink.Descriptors.xml` for trimmer roots
- `EmitCompilerGeneratedFiles` to inspect output under `obj/Generated/Abp.SourceGenerators/`

#### 7. What stays on the Castle path (for now)

`Abp.Web.Common` references `Abp.Interception.Castle` for the default stack. Compile-time interception replaces **runtime DynamicProxy for registered services** only when explicitly enabled via `CompileTimeInterceptionConfiguration.Enable()`. IoC remains Castle.Windsor until roadmap Step 2.

#### 8. Verify

Run [`test/Abp.Interception.CompileTime.Tests`](test/Abp.Interception.CompileTime.Tests) against [`test/Abp.Interception.CompileTime.Host`](test/Abp.Interception.CompileTime.Host):

| Test class | What it checks |
|:-----------|:---------------|
| `TaggedCompileTimeInterceptorWebTests` | User interceptor via class-bridge (`AbpInterceptorBase` + `IAbpInvocation`) |
| `StructAllocationFreeInterceptorWebTests` | User interceptor via allocation-free struct path (`AbpInterceptorBaseAllocationFree`) |
| `BuiltInInterceptorWebTests` | Built-in auditing with user interceptors in the same chain |

With `EmitCompilerGeneratedFiles` on the host project, generated sources appear under `obj/Generated/Abp.SourceGenerators/`.

### 한국어

이 저장소는 [ASP.NET Boilerplate](https://github.com/aspnetboilerplate/aspnetboilerplate)의 포크이며, 원본 프로젝트의 방향과는 별도의 목표를 가지고 유지·발전시킵니다.

**목표**

1. **NativeAOT** — AOT(Ahead-of-Time) 컴파일과 호환되도록 프레임워크를 재구성합니다. 런타임 코드 생성·동적 어셈블리 로딩에 의존하는 부분을 제거하거나 컴파일 타임 대안으로 대체하는 것이 핵심입니다.
2. **최신화** — .NET, 의존성 패키지, 빌드·배포 방식을 현재 표준에 맞게 갱신합니다.
3. **AutoMapper 제거 및 대체** — `Abp.AutoMapper` 연동과 AutoMapper 자체를 제거합니다. 객체 간 매핑은 소스 생성기 등 컴파일 타임 방식으로 대체하여 AOT과 호환되고 런타임 리플렉션 기반 설정이 없도록 합니다.

**마이그레이션 로드맵**

이 목표들을 달성하기 위해 **Castle.Windsor**를 가장 먼저 제거하고, 이어서 **AutoMapper**를 제거합니다. ABP는 IoC와 인터셉션(감사, 유효성 검사, 단위 of work 등)에 Castle 스택에, 애플리케이션 서비스의 DTO 매핑에 AutoMapper에 깊이 의존하고 있습니다. 둘 다 런타임 리플렉션과 동적 설정에 기반하며, NativeAOT와 최신 .NET 생태계와의 괴리를 만드는 주요 원인입니다.

| 단계 | 작업 | 설명 |
|:-----|:-----|:-----|
| 1 | **DynamicProxy → 소스 생성기** | `Castle.DynamicProxy` 기반 런타임 프록시를 소스 생성기(Source Generator)로 대체합니다. 인터셉터·프록시 로직을 컴파일 타임에 생성하여 AOT 트리밍과 호환되게 합니다. |
| 2 | **IoC 대체 → Castle.Windsor 제거** | `Microsoft.Extensions.DependencyInjection` 등 표준 DI 컨테이너로 IoC를 이전한 뒤 Castle.Windsor 및 관련 패키지를 완전히 제거합니다. |
| 3 | **AutoMapper → 소스 생성기** | `AutoMapper` / `Abp.AutoMapper`를 컴파일 타임 매핑(소스 생성기)으로 대체합니다. 런타임 프로필 스캔과 expression-tree 매핑을 제거하고, 생성된 `Map` 메서드로 전환합니다. |

```
Castle.Windsor 제거
    ├── 1단계: DynamicProxy → Source Generator
    └── 2단계: IoC 대체 (MS.DI 등)
            └── NativeAOT · 최신화
AutoMapper 제거
    └── 3단계: AutoMapper → Source Generator
            └── NativeAOT · 최신화
```

**컴파일 타임 인터셉션 마이그레이션 (1단계)**

애플리케이션 서비스 및 기타 대상 타입의 인터셉션을 Castle `DynamicProxy`에서 `Abp.SourceGenerators` 기반 compile-time 경로로 옮기는 방법입니다. Castle 경로와 compile-time 경로는 **동시에 사용하지 않습니다**. compile-time 인터셉션을 켜면 Castle 인터셉터 등록기는 비활성화되고, 인터셉션 로직은 C# 소스로 생성됩니다.

**참고 샘플 (NativeAOT):** [`test/Abp.Interception.CompileTime.Host`](test/Abp.Interception.CompileTime.Host) — `<PublishAot>true</PublishAot>` compile-time 인터셉션 호스트. AOT 런타임은 아직 완전하지 않음(Castle Windsor); 로드맵 2단계 전까지는 JIT `dotnet run`으로 수동 확인.

**1. 프로젝트 참조 추가**

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Abp.SourceGenerators.Runtime/Abp.SourceGenerators.Runtime.csproj" />
  <ProjectReference Include="path/to/Abp.SourceGenerators/Abp.SourceGenerators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

생성 코드 확인(선택):

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

**2. 시작 시 compile-time 인터셉션 활성화 (`Initialize` 이전)**

Castle 프록시 등록은 `AbpModule.Initialize()` **보다 먼저** 꺼야 합니다.

```csharp
return services.AddAbp<MyModule>(options =>
{
    CompileTimeInterceptionConfiguration.Enable();
});
```

또는 `AbpBootstrapper.CreateWithCompileTimeInterception<TModule>()`를 사용합니다.

`CompileTimeInterceptionConfiguration.Enable()`은 validation, auditing, unit of work, authorization, entity history용 Castle 등록기를 비활성화합니다.

**3. 모듈에서 compile-time convention 등록 사용**

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

소스 생성기가 `partial` `RegisterAssemblyByConvention`을 생성하여 다음을 등록합니다.

- 인터셉션 대상 타입(앱 서비스 등) → `{Service}_Intercepted` 데코레이터
- 사용자 인터셉터 (`AbpInterceptorBase` + `ITransientDependency` + `[AbpInterceptor]` 또는 `[AbpIntercept]`)

생성된 등록기가 없으면 런타임 `RegisterAssemblyByConvention(assembly)`로 폴백합니다.

**기존 애플리케이션에서 마이그레이션**

일반적인 ABP 앱은 `Abp.Interception.Castle`을 통해 Castle `DynamicProxy`를 사용합니다. convention 등록으로 애플리케이션 서비스가 등록되고, Windsor이 resolve 시점에 `AbpAsyncDeterminationInterceptor<T>`를 붙입니다. compile-time 경로는 **런타임 프록시 레이어만** 대체합니다. IoC는 로드맵 2단계까지 Castle.Windsor를 유지합니다.

**체크리스트**

| 단계 | 이전 (Castle) | 이후 (compile-time) |
|:-----|:--------------|:--------------------|
| 참조 | `Abp` / `Abp.AspNetCore`만 | `Abp.SourceGenerators.Runtime` + `Abp.SourceGenerators` analyzer 추가 |
| 시작 | `services.AddAbp<MyModule>()` | `AddAbp` 옵션에서 `CompileTimeInterceptionConfiguration.Enable()` 호출 (`Initialize` 이전) |
| 모듈 클래스 | `public class MyModule` | `public partial class MyModule` |
| Convention 등록 | `IocManager.RegisterAssemblyByConvention(typeof(MyModule).Assembly)` | `using Abp.Dependency.CompileTime;` 후 `IocManager.RegisterAssemblyByConvention(typeof(MyModule))` |
| 앱 서비스 resolve | 구현 타입 + Castle 프록시 | IoC가 생성된 `{Service}_Intercepted` 데코레이터 resolve |
| 사용자 인터셉터 | Castle 전용 wiring | 애플리케이션 서비스와 **같은 어셈블리**의 `AbpInterceptorBase` + `ITransientDependency` + `[AbpInterceptor(typeof(TriggerAttribute))]` (또는 `[AbpIntercept]`) |

`IApplicationService` 구현 클래스는 대부분 **코드 변경 없이** 동작합니다. `[AbpAuthorize]`, `[Audited]`, `[UnitOfWork]`, `[DisableValidation]`, `[UseCase]` 등 내장 aspect는 compile-time에 분석되어 `AbpMethodInterceptionMetadata`로 bake되며, 생성된 `{Name}_Intercepted` 정적 생성자가 `AbpMethodInterceptionMetadataProvider`에 등록합니다.

**프로젝트 역할**

| 프로젝트 | 역할 |
|:---------|:-----|
| `Abp.SourceGenerators` | Roslyn 소스 생성기. `{Service}_Intercepted`, 모듈 IoC partial, 메타데이터 등록 코드 생성. |
| `Abp.SourceGenerators.Runtime` | `Abp.Dependency.CompileTime` 네임스페이스의 런타임 지원 (`CompileTimeInterceptionConfiguration`, `AbpInvocationCompileTime`, `AbpInvocationStruct`, `AbpInterceptorBaseAllocationFree`, `IAbpInterceptorSync` / Task / ValueTask async 인터페이스, IoC 확장). |
| `Abp` | 공유 런타임 모델: `AbpMethodInfo`, `AbpMethodInterceptionMetadata`, `AbpMethodInterceptionMetadataProvider`, `IAbpInvocation`, 메타데이터 fast path를 가진 프레임워크 헬퍼. |
| `Abp.Interception.Castle` | compile-time 인터셉션이 **비활성**일 때의 기본 Castle DynamicProxy 경로. |

`Abp.SourceGenerators.Runtime` 폴더 구조는 `Abp`와 대응됩니다: `Dependency/CompileTime/`, `Runtime/Validation/Interception/`, 프로젝트 루트의 `AbpBootstrapperCompileTimeExtensions.cs`.

**API 및 추상화 변경**

Castle 리플렉션과 compile-time 메타데이터가 동일한 런타임 API를 공유합니다. **class-bridge 사용자 인터셉터는 `IAbpInvocation`을, allocation-free 사용자 인터셉터는 스택 `AbpInvocationStruct`의 `protected Internal*`를 override합니다.** 프레임워크 헬퍼는 기존 `MethodInfo` 기반 인터페이스를 유지하며 bake된 메타데이터가 있으면 우선 사용합니다.

| 영역 | 이전 | 이후 |
|:-----|:-----|:-----|
| Invocation | `Castle.DynamicProxy.IInvocation` | `IAbpInvocation` (`Abp.Dependency`) |
| 인터셉터의 메서드 | `invocation.Method` | `invocation.MethodInvocationTarget` (메타데이터가 있으면 `AbpMethodInfo`일 수 있음) |
| 비동기 proceed | Castle `Proceed()` | `invocation.CaptureProceedInfo().Invoke()` 후 `invocation.ReturnValue`를 `Task` / `Task<T>`로 await |
| 인터셉터 베이스 | `AbpInterceptorBase` | 동일; override는 `IAbpInvocation` 사용 |
| 내장 aspect 메타데이터 | 런타임 `MethodInfo` 리플렉션 | compile-time: `AbpMethodInterceptionMetadata`로 bake, `AbpMethodInfo.TryGetMetadata(method, out metadata)`로 조회 |
| 헬퍼 등록 | `ITransientDependency` convention | 동일; Castle registrar는 인터셉터 **프록시**만 등록 |

`AbpInvocationExtensions.GetMethodInvocationTarget` / `GetAbpMethod`는 `invocation.MethodInvocationTarget`을 반환합니다.

**프레임워크 헬퍼** (`AuthorizationHelper`, `AuditingHelper`, `MethodInvocationValidator`, `EntityHistoryUseCaseDescriptionProvider`)는 `Abp`에 있으며 기존 `MethodInfo` 기반 인터페이스를 구현합니다. 각각 `AbpMethodInfo.TryGetMetadata`를 먼저 확인하고, bake된 메타데이터가 없으면 기존 리플렉션 경로로 폴백합니다 (Castle 호환).

**메타데이터 모델** (`Abp.Dependency`):

- `AbpMethodInterceptionMetadata` — bake된 필드 (`ShouldAudit`, `ShouldValidate`, `UnitOfWorkAttribute`, `AuthorizeAttributes`, …)
- `AbpMethodInterceptionMetadataProvider.Instance` — 생성된 `{Name}_Intercepted` 정적 생성자가 채우는 런타임 레지스트리
- `AbpMethodInfo` — `MethodInfo` 서브클래스; `GetInvocationMethod` / `TryGetMetadata`로 Castle·compile-time 경로 통합

생성 코드는 `nameof(Type.Method)`로 메서드 이름을 해석하여 리팩터링에 안전합니다.

**Compile-time 속성** (`Abp.Dependency.CompileTime`, `Abp.SourceGenerators.Runtime`에 정의)

| 속성 | 용도 |
|:-----|:-----|
| `[AbpReflection(Include = true/false)]` | 타입·메서드의 메타데이터 bake 및 compile-time 인터셉션 opt-in/out. 앱 서비스와 내장 aspect가 있는 타입은 기본 bake. |
| `[AbpInterceptor(typeof(TriggerAttribute))]` | `AbpInterceptorBase` 구현체에 선언. 트리거 속성과 인터셉터 매핑. 사용자 인터셉터에 **필수**; 없으면 컴파일 오류 `ABPCT001`. `AllowMultiple = true`. |
| `[AbpIntercept(typeof(MyInterceptor))]` | 클래스·메서드에 직접 인터셉터 연결 (트리거 속성 패턴 대안). |
| `[DisableConventionalRegistration]` | compile-time 어셈블리 스캔 제외 (생성된 `{Name}_Intercepted` 타입이 사용). |

**Convention 등록 API** — compile-time 확장 메서드는 `Abp.Dependency.CompileTime`에 있으며 인자는 `Assembly`가 아니라 **모듈 타입**입니다.

```csharp
// 이전
IocManager.RegisterAssemblyByConvention(typeof(MyModule).Assembly);

// 이후
using Abp.Dependency.CompileTime;

public partial class MyModule : AbpModule
{
    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(MyModule));
    }
}
```

**사용자 인터셉터** — Castle 타입에서 `IAbpInvocation`으로 이전합니다.

```csharp
// 이전 (Castle 전용)
public override void InterceptSynchronous(IInvocation invocation) { ... }

// 이후
public override void InterceptSynchronous(IAbpInvocation invocation)
{
    var method = invocation.MethodInvocationTarget;
    var attr = method.GetCustomAttributes<MyAttribute>(inherit: true);
    invocation.Proceed();
}
```

사용자 인터셉터는 Windsor 인터셉터 목록으로 더 이상 등록되지 않습니다. `AbpInterceptorBase`, `ITransientDependency`, 최소 하나의 `[AbpInterceptor(typeof(TriggerAttribute))]` (또는 대상의 `[AbpIntercept]`)를 구현하고, 스캔 대상 서비스와 같은 어셈블리에 두어야 합니다.

**제거·중단해도 되는 것**

- 애플리케이션 서비스용 `AbpAsyncDeterminationInterceptor<T>` 수동 등록 (compile-time은 생성된 데코레이터 사용).
- 애플리케이션 코드에서 Castle `IInvocation` 의존.
- compile-time 인터셉션 활성화 후 `IApplicationService`에 DynamicProxy가 붙을 것이라는 가정.

**변하지 않는 것**

- 모듈 구조, `DependsOn`, `PreInitialize` / `PostInitialize`.
- 애플리케이션 서비스 인터페이스와 DTO.
- 내장 인터셉터 **클래스** (`AuthorizationInterceptor`, `UnitOfWorkInterceptor` 등) — IoC에서 여전히 resolve; **연결 방식**만 변경.
- IoC 컨테이너로서의 Castle.Windsor (2단계까지).

**4. 생성물**

compile-time 인터셉션이 필요한 각 타입에 대해 생성기가 다음을 만듭니다:

| 생성물 | 역할 |
|--------|------|
| `{Name}_Intercepted` | IoC 등록 데코레이터 (`ITransientDependency`, `[DisableConventionalRegistration]`); 정적 생성자가 `AbpMethodInterceptionMetadata` 등록 |
| `{Module}.CompileTime.g.cs` | `RegisterAssemblyByConvention(IIocManager)` 모듈 partial |

**대상 조건** (다음 중 하나):

- `IApplicationService` 구현 (항상 compile-time 인터셉션)
- 타입·메서드의 `[AbpReflection]`
- 타입·메서드의 내장 aspect 속성 (`Audited`, `UnitOfWork`, `AbpAuthorize`, `RequiresFeature`, `UseCase`, …)
- `[AbpIntercept]` 또는 사용자 인터셉터 트리거 속성

인터셉터 실행 순서:

```
Validation → Auditing → EntityHistory → UnitOfWork → Authorization → 사용자 인터셉터 → 대상 메서드
```

지원 반환 형식: `void`, 동기 `T`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`.

**compile-time 실행 경로**

생성된 메서드는 스택 `AbpInvocationStruct` / `AbpInvocationStruct<TAsync>`와 `CompileTimeInvocationInterceptorExecutor`를 통해 실행됩니다. 내장 인터셉터(`AuthorizationInterceptor`, `AuditingInterceptor` 등)는 `AbpInterceptorBase`만 상속하며 항상 **class-bridge** 경로(`AbpInvocationCompileTime` / `IAbpInvocation`)를 사용합니다.

사용자 인터셉터는 레이어별로 다음처럼 라우팅됩니다.

| 반환 형태 | class-bridge (`AbpInterceptorBase`) | allocation-free (`AbpInterceptorBaseAllocationFree`) |
|:----------|:-----------------------------------|:-------------------------------------------------------|
| sync | `RunSyncLayer` → `InterceptSynchronous(IAbpInvocation)` | `RunSyncAllocationFreeLayer` → `IAbpInterceptorSync` → `protected InternalInterceptSynchronous(ref AbpInvocationStruct)` |
| `Task` / `Task<T>` | `RunTaskViaClassBridgeLayer` → `InternalInterceptAsynchronous(IAbpInvocation)` | `RunTaskAllocationFreeLayer` → `IAbpInterceptorTaskAsync` → struct `Internal*` |
| `ValueTask` / `ValueTask<T>` | `RunValueTaskViaClassBridgeLayer` (Task 호환 브리지) | `RunValueTaskAllocationFreeLayer`, 또는 Task struct `Internal*`만 있을 때 `RunValueTaskViaTaskStructLayer` |

`AbpInvocationCompileTimeAsyncBridge`는 내장·사용자 인터셉터가 한 체인에 섞일 때 struct 레이어와 class-bridge 인터셉터를 연결합니다. allocation-free 인터셉터에는 compile-time 경로에서 `IAbpInterceptorSync` / `IAbpInterceptorTaskAsync` / `IAbpInterceptorValueTaskAsync`를 직접 호출합니다 (`IAbpInvocation` 미사용).

생성기는 `AbpInterceptorBaseAllocationFree`에서 proceed-only가 아닌 `protected Internal*` struct override가 있으면 allocation-free 라우팅을 선택합니다 (`AllocationFreeInterceptorAnalyzer`).

**5. 사용자 정의 인터셉터**

**class-bridge** (기본 권장; 내장 인터셉터와 동일 패턴). `test/Abp.Interception.CompileTime.Host/Interceptors/TaggedCompileTimeInterceptor.cs` 참고:

```csharp
[AbpInterceptor(typeof(TaggedAttribute))]
public sealed class TaggedCompileTimeInterceptor : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation) { /* ... */ }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation) { /* ... */ }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation) { /* ... */ }
}
```

`ValueTask` 반환 애플리케이션 메서드도 동일한 `InternalInterceptAsynchronous` / `InternalInterceptAsynchronous<TResult>`로 진입하며, 필요 시 executor가 브리지에서 `ValueTask`를 `Task`로 materialize합니다.

**allocation-free** (선택; 사용자 인터셉터 레이어에서 heap 할당 감소). `AbpInterceptorBaseAllocationFree`를 상속하고 `protected Internal*` struct 메서드만 override합니다. public `InterceptSynchronous(ref …)` / `InterceptAsynchronous(ref …)`는 override하지 않습니다. `test/Abp.Interception.CompileTime.Host/Interceptors/StructTaggedCompileTimeInterceptor.cs` 참고:

```csharp
[AbpInterceptor(typeof(StructTaggedAttribute))]
public sealed class StructTaggedCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation) { /* ... */ }

    protected override Task InternalInterceptAsynchronous(ref AbpInvocationStruct<Task> invocation) { /* ... */ }

    protected override Task<TResult> InternalInterceptAsynchronous<TResult>(ref AbpInvocationStruct<Task<TResult>> invocation) { /* ... */ }

    protected override ValueTask InternalInterceptAsynchronous(ref AbpInvocationStruct<ValueTask> invocation) { /* ... */ }

    protected override ValueTask<TResult> InternalInterceptAsynchronous<TResult>(ref AbpInvocationStruct<ValueTask<TResult>> invocation) { /* ... */ }
}
```

`AbpInterceptorBaseAllocationFree`는 Castle / legacy `IAbpInvocation` 진입점도 스택 struct로 어댑트한 뒤 동일한 `protected Internal*`로 전달합니다.

트리거 속성 예:

```csharp
[Tagged("demo")]
public class MyAppService : ApplicationService { /* ... */ }
```

클래스·메서드에 `[AbpIntercept(typeof(MyInterceptor))]`를 붙이는 직접 연결도 지원합니다.

- **앱 서비스**: 발견된 모든 사용자 인터셉터가 체인에 포함됩니다.
- **비–앱 서비스**: 트리거 또는 `[AbpIntercept]`가 일치하는 인터셉터만 적용됩니다.

생성기가 compile-time에 타입을 찾아 IoC 등록과 인터셉터 체인에 bake합니다. 런타임 어셈블리 스캔은 사용하지 않습니다.

**6. NativeAOT 게시 (선택)**

샘플 프로젝트 참고: `<PublishAot>true</PublishAot>`, `ILLink.Descriptors.xml`, `EmitCompilerGeneratedFiles`로 `obj/Generated/Abp.SourceGenerators/`에서 생성 코드 확인.

**7. 아직 Castle에 남는 부분**

`Abp.Web.Common`은 기본 스택을 위해 `Abp.Interception.Castle`을 참조합니다. compile-time 인터셉션은 `CompileTimeInterceptionConfiguration.Enable()`로 **명시적으로 켠 경우** 등록된 서비스의 DynamicProxy만 대체합니다. IoC는 로드맵 2단계까지 Castle.Windsor를 유지합니다.

**8. 검증**

[`test/Abp.Interception.CompileTime.Tests`](test/Abp.Interception.CompileTime.Tests) 실행 — [`test/Abp.Interception.CompileTime.Host`](test/Abp.Interception.CompileTime.Host) 대상 통합 테스트:

| 테스트 클래스 | 검증 내용 |
|:--------------|:----------|
| `TaggedCompileTimeInterceptorWebTests` | class-bridge 사용자 인터셉터 (`AbpInterceptorBase` + `IAbpInvocation`) |
| `StructAllocationFreeInterceptorWebTests` | allocation-free struct 경로 (`AbpInterceptorBaseAllocationFree`) |
| `BuiltInInterceptorWebTests` | 내장 auditing과 사용자 인터셉터가 같은 체인에서 동작 |

호스트 프로젝트에 `EmitCompilerGeneratedFiles`가 켜져 있으면 생성 코드는 `obj/Generated/Abp.SourceGenerators/`에서 볼 수 있습니다.

> ### End of Support Announcement
> Support for ASP.NET Boilerplate will officially end in **May 2026**. However, we will continue to provide support for [ASP.NET Zero](https://aspnetzero.com/?utm_source=referral&utm_medium=github&utm_campaign=github_zerowebsite_redirection) **customers** using ASP.NET Boilerplate. For those looking for an open-source alternative, we recommend migrating to [ABP Framework](https://abp.io/?utm_source=referral&utm_medium=github&utm_campaign=github_abpwebsite_redirection). For the full story, [read the end of life announcement](https://aspnetboilerplate.com/endofsupport?utm_source=referral&utm_medium=github&utm_campaign=github_zboilerplate_announcement_redirection).

## What is ABP?

[ASP.NET Boilerplate](https://aspnetboilerplate.com) is a general purpose **application framework** specially designed for new modern web applications. It uses already **familiar tools** and implements **best practices** around them to provide you a **SOLID development experience**.

ASP.NET Boilerplate works with the latest **ASP.NET Core** & **EF Core** but also supports ASP.NET MVC 5.x & EF 6.x as well.

###### Modular Design

Designed to be <a href="https://aspnetboilerplate.com/Pages/Documents/Module-System" target="_blank">**modular**</a> and **extensible**, ABP provides the infrastructure to build your own modules, too.

###### Multi-Tenancy

**SaaS** applications made easy! Integrated <a href="https://aspnetboilerplate.com/Pages/Documents/Multi-Tenancy" target="_blank">multi-tenancy</a> from database to UI.

###### Well-Documented

Comprehensive <a href="https://aspnetboilerplate.com/Pages/Documents" target="_blank">**documentation**</a> and quick start tutorials.

## How It Works

Don't Repeat Yourself! ASP.NET Boilerplate automates common software development tasks by convention. You focus on your business code!

![ASP.NET Boilerplate](doc/img/abp-concerns.png)

See the <a href="https://aspnetboilerplate.com/Pages/Documents/Introduction" target="_blank">Introduction</a> document for more details.

## Layered Architecture

ABP provides a layered architectural model based on **Domain Driven Design** and provides a **SOLID** model for your application.

![NLayer Architecture](doc/img/abp-nlayer-architecture.png)

See the <a href="https://aspnetboilerplate.com/Pages/Documents/NLayer-Architecture" target="_blank">NLayer Architecture</a> document for more details.

## Nuget Packages

ASP.NET Boilerplate is distributed as NuGet packages.

|Package|Status|
|:------|:-----:|
|Abp|[![NuGet version](https://badge.fury.io/nu/Abp.svg)](https://badge.fury.io/nu/Abp)|
|Abp.AspNetCore|[![NuGet version](https://badge.fury.io/nu/Abp.AspNetCore.svg)](https://badge.fury.io/nu/Abp.AspNetCore)|
|Abp.Web.Common|[![NuGet version](https://badge.fury.io/nu/Abp.Web.Common.svg)](https://badge.fury.io/nu/Abp.Web.Common)|
|Abp.Web.Resources|[![NuGet version](https://badge.fury.io/nu/Abp.Web.Resources.svg)](https://badge.fury.io/nu/Abp.Web.Resources)|
|Abp.EntityFramework.Common|[![NuGet version](https://badge.fury.io/nu/Abp.EntityFramework.Common.svg)](https://badge.fury.io/nu/Abp.EntityFramework.Common)|
|Abp.EntityFramework|[![NuGet version](https://badge.fury.io/nu/Abp.EntityFramework.svg)](https://badge.fury.io/nu/Abp.EntityFramework)|
|Abp.EntityFrameworkCore|[![NuGet version](https://badge.fury.io/nu/Abp.EntityFrameworkCore.svg)](https://badge.fury.io/nu/Abp.EntityFrameworkCore)|
|Abp.NHibernate|[![NuGet version](https://badge.fury.io/nu/Abp.NHibernate.svg)](https://badge.fury.io/nu/Abp.NHibernate)|
|Abp.Dapper|[![NuGet version](https://badge.fury.io/nu/Abp.Dapper.svg)](https://badge.fury.io/nu/Abp.Dapper)|
|Abp.FluentMigrator|[![NuGet version](https://badge.fury.io/nu/Abp.FluentMigrator.svg)](https://badge.fury.io/nu/Abp.FluentMigrator)|
|Abp.AspNetCore|[![NuGet version](https://badge.fury.io/nu/Abp.AspNetCore.svg)](https://badge.fury.io/nu/Abp.AspNetCore)|
|Abp.AspNetCore.SignalR|[![NuGet version](https://badge.fury.io/nu/Abp.AspNetCore.SignalR.svg)](https://badge.fury.io/nu/Abp.AspNetCore.SignalR)|
|Abp.AutoMapper|[![NuGet version](https://badge.fury.io/nu/Abp.AutoMapper.svg)](https://badge.fury.io/nu/Abp.AutoMapper)|
|Abp.HangFire|[![NuGet version](https://badge.fury.io/nu/Abp.HangFire.svg)](https://badge.fury.io/nu/Abp.HangFire)|
|Abp.HangFire.AspNetCore|[![NuGet version](https://badge.fury.io/nu/Abp.HangFire.AspNetCore.svg)](https://badge.fury.io/nu/Abp.HangFire.AspNetCore)|
|Abp.Castle.Log4Net|[![NuGet version](https://badge.fury.io/nu/Abp.Castle.Log4Net.svg)](https://badge.fury.io/nu/Abp.Castle.Log4Net)|
|Abp.RedisCache|[![NuGet version](https://badge.fury.io/nu/Abp.RedisCache.svg)](https://badge.fury.io/nu/Abp.RedisCache)|
|Abp.RedisCache.ProtoBuf|[![NuGet version](https://badge.fury.io/nu/Abp.RedisCache.ProtoBuf.svg)](https://badge.fury.io/nu/Abp.RedisCache.ProtoBuf)|
|Abp.MailKit|[![NuGet version](https://badge.fury.io/nu/Abp.MailKit.svg)](https://badge.fury.io/nu/Abp.MailKit)|
|Abp.Quartz|[![NuGet version](https://badge.fury.io/nu/Abp.Quartz.svg)](https://badge.fury.io/nu/Abp.Quartz)|
|Abp.TestBase|[![NuGet version](https://badge.fury.io/nu/Abp.TestBase.svg)](https://badge.fury.io/nu/Abp.TestBase)|
|Abp.AspNetCore.TestBase|[![NuGet version](https://badge.fury.io/nu/Abp.AspNetCore.TestBase.svg)](https://badge.fury.io/nu/Abp.AspNetCore.TestBase)|
|Abp.AspNetCore.OpenIddict|[![NuGet version](https://badge.fury.io/nu/Abp.AspNetCore.OpenIddict.svg)](https://badge.fury.io/nu/Abp.AspNetCore.OpenIddict)|

# Module Zero

## What is 'Module Zero'?

This is an <a href="https://aspnetboilerplate.com/" target="_blank">ASP.NET Boilerplate</a> module integrated with Microsoft <a href="https://docs.microsoft.com/en-us/aspnet/identity/overview/getting-started/introduction-to-aspnet-identity" target="_blank">ASP.NET Identity</a>.

Implements abstract concepts of ASP.NET Boilerplate framework:

* <a href="https://aspnetboilerplate.com/Pages/Documents/Setting-Management" target="_blank">Setting store</a>
* <a href="https://aspnetboilerplate.com/Pages/Documents/Audit-Logging" target="_blank">Audit log store</a>
* <a href="https://aspnetboilerplate.com/Pages/Documents/Background-Jobs-And-Workers" target="_blank">Background job store</a>
* <a href="https://aspnetboilerplate.com/Pages/Documents/Feature-Management" target="_blank">Feature store</a>
* <a href="https://aspnetboilerplate.com/Pages/Documents/Notification-System" target="_blank">Notification store</a>
* <a href="https://aspnetboilerplate.com/Pages/Documents/Authorization" target="_blank">Permission checker</a>

Also adds common enterprise application features:

* **<a href="https://aspnetboilerplate.com/Pages/Documents/Zero/User-Management" target="_blank">User</a>, <a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Role-Management" target="_blank">Role</a> and <a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Permission-Management" target="_blank">Permission</a>** management for applications that require authentication and authorization.
* **<a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Tenant-Management" target="_blank">Tenant</a> and <a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Edition-Management" target="_blank">Edition</a>** management for SaaS applications.
* **<a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Organization-Units" target="_blank">Organization Units</a>** management.
* **<a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Language-Management" target="_blank">Language and localization</a> text** management.
* **<a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Identity-Server" target="_blank">Identity Server 4</a>** integration.

Module Zero packages define entities and implement base domain logic for these concepts.

## NuGet Packages

### ASP.NET Core Identity Packages

Packages integrated into <a href="https://docs.microsoft.com/en-us/aspnet/identity/overview/getting-started/introduction-to-aspnet-identity" target="_blank">ASP.NET Core Identity</a>.

|Package|Status|
|:------|:-----:|
|Abp.ZeroCore|[![NuGet version](https://badge.fury.io/nu/Abp.ZeroCore.svg)](https://badge.fury.io/nu/Abp.ZeroCore)|
|Abp.ZeroCore.EntityFrameworkCore|[![NuGet version](https://badge.fury.io/nu/Abp.ZeroCore.EntityFrameworkCore.svg)](https://badge.fury.io/nu/Abp.ZeroCore.EntityFrameworkCore)|

### Shared Packages

Shared packages between the Abp.ZeroCore.\* and Abp.Zero.\* packages.

|Package|Status|
|:------|:-----:|
|Abp.Zero.Common|[![NuGet version](https://badge.fury.io/nu/Abp.Zero.Common.svg)](https://badge.fury.io/nu/Abp.Zero.Common)|
|Abp.Zero.Ldap|[![NuGet version](https://badge.fury.io/nu/Abp.Zero.Ldap.svg)](https://badge.fury.io/nu/Abp.Zero.Ldap)|

## Startup Templates

You can create your project from startup templates to easily start with Module Zero:

* <a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Startup-Template-Angular" target="_blank">ASP.NET Core & Angular</a> based startup project.
* <a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Startup-Template-Core" target="_blank">ASP.NET Core MVC & jQuery</a> based startup project.
* <a href="https://aspnetboilerplate.com/Pages/Documents/Zero/Startup-Template" target="_blank">ASP.NET Core MVC 5.x / AngularJS</a> based startup project.

A screenshot of the ASP.NET Core based startup template:

![](doc/img/module-zero-core-template-1.png)

## Links

* Web site & Documentation: https://aspnetboilerplate.com
* Questions & Answers: https://stackoverflow.com/questions/tagged/aspnetboilerplate?sort=newest

## Code of Conduct

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct). 

### .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).

## License

[MIT](LICENSE).
