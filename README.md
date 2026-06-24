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

This section describes how to move **application service interception** from Castle `DynamicProxy` to the `Abp.SourceGenerators` compile-time path. The Castle and compile-time paths are mutually exclusive for a given application: when compile-time interception is enabled, Castle interceptor registrars are disabled and interception is emitted as C# source.

**Reference sample:** [`test/Abp.NativeAot.SampleWebApp`](test/Abp.NativeAot.SampleWebApp)

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

- Application services → `{Service}_Intercepted` decorator types
- User-defined interceptors (`AbpInterceptorBase` + `ITransientDependency`)

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
| Custom interceptors | Often Castle-specific wiring | `AbpInterceptorBase` + `ITransientDependency` in the **same assembly** as application services |

Application service classes (`IApplicationService` implementations) usually need **no attribute or signature changes**. Attributes such as `[AbpAuthorize]`, `[Audited]`, `[UnitOfWork]`, and `[DisableValidation]` are read at compile time and baked into `CompileTimeAbpMethodInfo`.

#### API and interface changes

To support both Castle reflection and compile-time metadata, several core APIs were unified. **Custom interceptors and helpers should use the new abstractions** so they work on the compile-time path.

| Area | Before | After |
|:-----|:-------|:------|
| Invocation | `Castle.DynamicProxy.IInvocation` (Castle-only) | `IAbpInvocation` (`Abp.Dependency`) |
| Method metadata in interceptors | `invocation.Method`, `MethodInfo` | `invocation.MethodInvocationTarget` (`AbpMethodInfo`), or `invocation.GetAbpMethod()` |
| Method reflection helper | Direct `MethodInfo` usage | `AbpInvocationExtensions.GetMethodInvocationTarget(invocation)` when you need `MethodInfo` on the Castle path |
| Async proceed | Castle `Proceed()` | `invocation.CaptureProceedInfo().Invoke()` then await `invocation.ReturnValue` as `Task` / `Task<T>` |
| Interceptor base | `AbpInterceptorBase` | Same type; all overrides use `IAbpInvocation` |
| Built-in aspect metadata | Read from `MethodInfo` at runtime | Baked into `IAbpBuiltInInterceptionMetadata` on compile-time path (framework helpers) |

**Helper interfaces** — new `AbpMethodInfo` overloads were added; Castle implementations keep the existing `MethodInfo` overloads:

| Interface | New overloads (used on compile-time path) |
|:----------|:------------------------------------------|
| `IAuthorizationHelper` | `Authorize(AbpMethodInfo, Type)`, `AuthorizeAsync(AbpMethodInfo, Type)` |
| `IAuditingHelper` | `ShouldSaveAudit(AbpMethodInfo, ...)`, `CreateAuditInfo(Type, AbpMethodInfo, ...)` |
| `IMethodInvocationValidator` | `Initialize(AbpMethodInfo, object[])` |

If you have a **custom** `IAuthorizationHelper`, `IAuditingHelper`, or `IMethodInvocationValidator`, implement the `AbpMethodInfo` overloads. On the compile-time path, `Method.ReflectionMethod` is often `null`; use `AbpMethodInfo.GetCustomAttributes<T>()` and, for built-in aspects, `method is IAbpBuiltInInterceptionMetadata builtIn`.

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

**Custom interceptors** — migrate from Castle types to `IAbpInvocation`:

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

User interceptors are no longer picked up by Windsor interceptor lists. They must inherit `AbpInterceptorBase`, implement `ITransientDependency`, and live in the same project as the application services.

**What you can remove or stop doing**

- Manual `AbpAsyncDeterminationInterceptor<T>` registration for application services.
- Relying on Castle `IInvocation` in application code.
- Expecting runtime convention registration to wrap `IApplicationService` types with DynamicProxy when compile-time interception is enabled.

**What stays the same**

- Module structure, `DependsOn`, `PreInitialize` / `PostInitialize`.
- Application service interfaces and DTOs.
- Built-in interceptor **classes** (`AuthorizationInterceptor`, `UnitOfWorkInterceptor`, etc.) — still resolved from IoC; only the **wiring** changes.
- Castle.Windsor as the IoC container (until Step 2).

#### 4. Application services (no code changes required)

`IApplicationService` implementations in the compiling assembly are analyzed at build time. For each service the generator emits:

| Artifact | Role |
|----------|------|
| `{Name}_Intercepted` | IoC-registered decorator implementing the service interface |
| `{Name}_Relay` | Static methods with inlined interceptor chains |
| `NativeAotSampleWebAppModule.CompileTime.g.cs` | IoC registrations |

Interceptor order in the generated chain:

```
Validation → Auditing → EntityHistory → UnitOfWork → Authorization → user interceptors → target method
```

Supported method return types: `void`, sync `T`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`.

#### 5. User-defined interceptors

Implement `AbpInterceptorBase` and `ITransientDependency` in the **same assembly** as the application services:

```csharp
public sealed class MyInterceptor : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation) { /* ... */ }
    protected override Task InternalInterceptAsynchronous(IAbpInvocation invocation) { /* ... */ }
    protected override Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation) { /* ... */ }
}
```

The generator discovers the type at compile time, registers it in IoC, and bakes it into the interceptor chain. No runtime assembly scanning is used.

#### 6. NativeAOT publishing (optional)

For full AOT publish, see the sample project:

- `<PublishAot>true</PublishAot>`
- `ILLink.Descriptors.xml` for trimmer roots
- Exclude generated files from compilation if emitted to `obj/Generated`

#### 7. What stays on the Castle path (for now)

`Abp.Web.Common` still references `Abp.Interception.Castle` for the default (non–compile-time) stack. Compile-time interception replaces **runtime DynamicProxy for application services** only when explicitly enabled. IoC remains Castle.Windsor until Step 2 of the roadmap.

#### 8. Verify

Run [`test/Abp.SourceGenerators.Tests`](test/Abp.SourceGenerators.Tests) or hit the sample endpoints after migration. Generated interception code lives under `obj/Generated/Abp.SourceGenerators/` when `EmitCompilerGeneratedFiles` is enabled.

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

애플리케이션 서비스 인터셉션을 Castle `DynamicProxy`에서 `Abp.SourceGenerators` 기반 compile-time 경로로 옮기는 방법입니다. Castle 경로와 compile-time 경로는 **동시에 사용하지 않습니다**. compile-time 인터셉션을 켜면 Castle 인터셉터 등록기는 비활성화되고, 인터셉션 로직은 C# 소스로 생성됩니다.

**참고 샘플:** [`test/Abp.NativeAot.SampleWebApp`](test/Abp.NativeAot.SampleWebApp)

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

- 애플리케이션 서비스 → `{Service}_Intercepted` 데코레이터
- 사용자 인터셉터 (`AbpInterceptorBase` + `ITransientDependency`)

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
| 사용자 인터셉터 | Castle 전용 wiring | 애플리케이션 서비스와 **같은 어셈블리**의 `AbpInterceptorBase` + `ITransientDependency` |

`IApplicationService` 구현 클래스는 대부분 **코드 변경 없이** 동작합니다. `[AbpAuthorize]`, `[Audited]`, `[UnitOfWork]`, `[DisableValidation]` 등은 compile-time에 분석되어 `CompileTimeAbpMethodInfo`에 bake됩니다.

**API 및 인터페이스 변경**

Castle 리플렉션과 compile-time 메타데이터를 함께 지원하기 위해 핵심 API가 통합되었습니다. **사용자 인터셉터·헬퍼는 새 추상화를 사용**해야 compile-time 경로에서도 동작합니다.

| 영역 | 이전 | 이후 |
|:-----|:-----|:-----|
| Invocation | `Castle.DynamicProxy.IInvocation` (Castle 전용) | `IAbpInvocation` (`Abp.Dependency`) |
| 인터셉터의 메서드 메타데이터 | `invocation.Method`, `MethodInfo` | `invocation.MethodInvocationTarget` (`AbpMethodInfo`), 또는 `invocation.GetAbpMethod()` |
| 리플렉션 헬퍼 | `MethodInfo` 직접 사용 | Castle 경로에서 `MethodInfo`가 필요하면 `AbpInvocationExtensions.GetMethodInvocationTarget(invocation)` |
| 비동기 proceed | Castle `Proceed()` | `invocation.CaptureProceedInfo().Invoke()` 후 `invocation.ReturnValue`를 `Task` / `Task<T>`로 await |
| 인터셉터 베이스 | `AbpInterceptorBase` | 동일; 모든 override가 `IAbpInvocation` 사용 |
| 내장 aspect 메타데이터 | 런타임 `MethodInfo`에서 읽음 | compile-time 경로에서는 `IAbpBuiltInInterceptionMetadata`로 bake (프레임워크 헬퍼 내부) |

**헬퍼 인터페이스** — `AbpMethodInfo` 오버로드가 추가되었습니다. Castle 구현체는 기존 `MethodInfo` 오버로드를 유지합니다.

| 인터페이스 | 추가된 오버로드 (compile-time 경로에서 사용) |
|:----------|:--------------------------------------------|
| `IAuthorizationHelper` | `Authorize(AbpMethodInfo, Type)`, `AuthorizeAsync(AbpMethodInfo, Type)` |
| `IAuditingHelper` | `ShouldSaveAudit(AbpMethodInfo, ...)`, `CreateAuditInfo(Type, AbpMethodInfo, ...)` |
| `IMethodInvocationValidator` | `Initialize(AbpMethodInfo, object[])` |

**커스텀** `IAuthorizationHelper`, `IAuditingHelper`, `IMethodInvocationValidator`가 있다면 `AbpMethodInfo` 오버로드를 구현하세요. compile-time 경로에서는 `Method.ReflectionMethod`가 `null`인 경우가 많으므로 `AbpMethodInfo.GetCustomAttributes<T>()`를 사용하고, 내장 aspect는 `method is IAbpBuiltInInterceptionMetadata builtIn`으로 확인합니다.

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

사용자 인터셉터는 Windsor 인터셉터 목록으로 더 이상 등록되지 않습니다. `AbpInterceptorBase`와 `ITransientDependency`를 구현하고, 소스 생성기가 스캔할 수 있도록 애플리케이션 서비스와 같은 프로젝트에 두어야 합니다.

**제거·중단해도 되는 것**

- 애플리케이션 서비스용 `AbpAsyncDeterminationInterceptor<T>` 수동 등록 (compile-time은 생성된 데코레이터 사용).
- 애플리케이션 코드에서 Castle `IInvocation` 의존.
- compile-time 인터셉션 활성화 후 `IApplicationService`에 DynamicProxy가 붙을 것이라는 가정.

**변하지 않는 것**

- 모듈 구조, `DependsOn`, `PreInitialize` / `PostInitialize`.
- 애플리케이션 서비스 인터페이스와 DTO.
- 내장 인터셉터 **클래스** (`AuthorizationInterceptor`, `UnitOfWorkInterceptor` 등) — IoC에서 여전히 resolve; **연결 방식**만 변경.
- IoC 컨테이너로서의 Castle.Windsor (2단계까지).

**4. 애플리케이션 서비스**

컴파일 대상 어셈블리의 `IApplicationService` 구현을 빌드 시 분석합니다. 생성물:

| 생성물 | 역할 |
|--------|------|
| `{Name}_Intercepted` | 서비스 인터페이스를 구현하는 IoC 데코레이터 |
| `{Name}_Relay` | 인터셉터 체인이 인라인된 static 메서드 |
| `{Module}.CompileTime.g.cs` | IoC 등록 코드 |

인터셉터 실행 순서:

```
Validation → Auditing → EntityHistory → UnitOfWork → Authorization → 사용자 인터셉터 → 대상 메서드
```

지원 반환 형식: `void`, 동기 `T`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`.

**5. 사용자 정의 인터셉터**

`AbpInterceptorBase`와 `ITransientDependency`를 **애플리케이션 서비스와 같은 어셈블리**에 구현합니다.

```csharp
public sealed class MyInterceptor : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation) { /* ... */ }
    protected override Task InternalInterceptAsynchronous(IAbpInvocation invocation) { /* ... */ }
    protected override Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation) { /* ... */ }
}
```

생성기가 compile-time에 타입을 찾아 IoC 등록과 인터셉터 체인에 bake합니다. 런타임 어셈블리 스캔은 사용하지 않습니다.

**6. NativeAOT 게시 (선택)**

샘플 프로젝트 참고: `<PublishAot>true</PublishAot>`, `ILLink.Descriptors.xml`, `obj/Generated` 제외 설정.

**7. 아직 Castle에 남는 부분**

`Abp.Web.Common`은 기본(비 compile-time) 스택을 위해 `Abp.Interception.Castle`을 참조합니다. compile-time 인터셉션은 **명시적으로 켠 경우** 애플리케이션 서비스의 DynamicProxy만 대체합니다. IoC는 로드맵 2단계까지 Castle.Windsor를 유지합니다.

**8. 검증**

[`test/Abp.SourceGenerators.Tests`](test/Abp.SourceGenerators.Tests) 실행 또는 샘플 API 호출. `EmitCompilerGeneratedFiles`가 켜져 있으면 생성 코드는 `obj/Generated/Abp.SourceGenerators/`에서 확인할 수 있습니다.

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
