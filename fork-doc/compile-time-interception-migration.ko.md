# 컴파일 타임 인터셉션 마이그레이션 (1단계)

애플리케이션 서비스 및 기타 대상 타입의 인터셉션을 Castle `DynamicProxy`에서 `Abp.SourceGenerators` 기반 compile-time 경로로 옮기는 방법입니다. Castle 경로와 compile-time 경로는 **동시에 사용하지 않습니다**. compile-time 인터셉션을 켜면 Castle 인터셉터 등록기는 비활성화되고, 인터셉션 로직은 C# 소스로 생성됩니다.

**참고 샘플 (NativeAOT):** [`test/Abp.Interception.CompileTime.Host`](../test/Abp.Interception.CompileTime.Host) — `<PublishAot>true</PublishAot>` compile-time 인터셉션 호스트. AOT 런타임은 아직 완전하지 않음(Castle Windsor); 로드맵 2단계 전까지는 JIT `dotnet run`으로 수동 확인.

## 1. 프로젝트 참조 추가

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

## 2. 시작 시 compile-time 인터셉션 활성화 (`Initialize` 이전)

Castle 프록시 등록은 `AbpModule.Initialize()` **보다 먼저** 꺼야 합니다.

```csharp
return services.AddAbp<MyModule>(options =>
{
    CompileTimeInterceptionConfiguration.Enable();
});
```

또는 `AbpBootstrapper.CreateWithCompileTimeInterception<TModule>()`를 사용합니다.

`CompileTimeInterceptionConfiguration.Enable()`은 validation, auditing, unit of work, authorization, entity history용 Castle 등록기를 비활성화합니다.

## 3. 모듈에서 compile-time convention 등록 사용

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

## 기존 애플리케이션에서 마이그레이션

일반적인 ABP 앱은 `Abp.Interception.Castle`을 통해 Castle `DynamicProxy`를 사용합니다. convention 등록으로 애플리케이션 서비스가 등록되고, Windsor이 resolve 시점에 `AbpAsyncDeterminationInterceptor<T>`를 붙입니다. compile-time 경로는 **런타임 프록시 레이어만** 대체합니다. IoC는 로드맵 2단계까지 Castle.Windsor를 유지합니다.

### 체크리스트

| 단계 | 이전 (Castle) | 이후 (compile-time) |
|:-----|:--------------|:--------------------|
| 참조 | `Abp` / `Abp.AspNetCore`만 | `Abp.SourceGenerators.Runtime` + `Abp.SourceGenerators` analyzer 추가 |
| 시작 | `services.AddAbp<MyModule>()` | `AddAbp` 옵션에서 `CompileTimeInterceptionConfiguration.Enable()` 호출 (`Initialize` 이전) |
| 모듈 클래스 | `public class MyModule` | `public partial class MyModule` |
| Convention 등록 | `IocManager.RegisterAssemblyByConvention(typeof(MyModule).Assembly)` | `using Abp.Dependency.CompileTime;` 후 `IocManager.RegisterAssemblyByConvention(typeof(MyModule))` |
| 앱 서비스 resolve | 구현 타입 + Castle 프록시 | IoC가 생성된 `{Service}_Intercepted` 데코레이터 resolve |
| 사용자 인터셉터 | Castle 전용 wiring | 애플리케이션 서비스와 **같은 어셈블리**의 `AbpInterceptorBase` + `ITransientDependency` + `[AbpInterceptor(typeof(TriggerAttribute))]` (또는 `[AbpIntercept]`) |

`IApplicationService` 구현 클래스는 대부분 **코드 변경 없이** 동작합니다. `[AbpAuthorize]`, `[Audited]`, `[UnitOfWork]`, `[DisableValidation]`, `[UseCase]` 등 내장 aspect는 compile-time에 분석되어 `AbpMethodInterceptionMetadata`로 bake되며, 생성된 `{Name}_Intercepted` 정적 생성자가 `AbpMethodInterceptionMetadataProvider`에 등록합니다.

## 프로젝트 역할

| 프로젝트 | 역할 |
|:---------|:-----|
| `Abp.SourceGenerators` | Roslyn 소스 생성기. `{Service}_Intercepted`, 모듈 IoC partial, 메타데이터 등록 코드 생성. |
| `Abp.SourceGenerators.Runtime` | `Abp.Dependency.CompileTime` 네임스페이스의 런타임 지원 (`CompileTimeInterceptionConfiguration`, `AbpInvocationCompileTime`, `AbpInvocationStruct`, `AbpInterceptorBaseAllocationFree`, `IAbpInterceptorSync` / Task / ValueTask async 인터페이스, IoC 확장). |
| `Abp` | 공유 런타임 모델: `AbpMethodInfo`, `AbpMethodInterceptionMetadata`, `AbpMethodInterceptionMetadataProvider`, `IAbpInvocation`, 메타데이터 fast path를 가진 프레임워크 헬퍼. |
| `Abp.Interception.Castle` | compile-time 인터셉션이 **비활성**일 때의 기본 Castle DynamicProxy 경로. |

`Abp.SourceGenerators.Runtime` 폴더 구조는 `Abp`와 대응됩니다: `Dependency/CompileTime/`, `Runtime/Validation/Interception/`, 프로젝트 루트의 `AbpBootstrapperCompileTimeExtensions.cs`.

## API 및 추상화 변경

Castle 리플렉션과 compile-time 메타데이터가 동일한 런타임 API를 공유합니다. **class-bridge 사용자 인터셉터는 `IAbpInvocation`을, allocation-free 사용자 인터셉터는 스택 `AbpInvocationStruct`의 `protected Internal*`를 override합니다.** 프레임워크 헬퍼는 기존 `MethodInfo` 기반 인터페이스를 유지하며 bake된 메타데이터가 있으면 우선 사용합니다.

| 영역 | 이전 | 이후 |
|:-----|:-----|:-----|
| Invocation | `Castle.DynamicProxy.IInvocation` | `IAbpInvocation` (`Abp.Dependency`) |
| 인터셉터의 메서드 | `invocation.Method` | `invocation.MethodInvocationTarget` (메타데이터가 있으면 `AbpMethodInfo`일 수 있음) |
| 비동기 proceed | Castle `Proceed()` | **class-bridge:** `invocation.CaptureProceedInfo().Invoke()` 후 `invocation.ReturnValue`를 `Task` / `Task<T>`로 await. **allocation-free fast path:** `return await invocation.Proceed()` (struct by value). **allocation-free 호환:** `CaptureProceedInfo()` → 작업 → `proceedInfo.Invoke()` (선택; delegate가 같으면 `Proceed()`와 동일). |
| 인터셉터 베이스 | `AbpInterceptorBase` | 동일; class-bridge override는 `IAbpInvocation`. allocation-free: `AbpInterceptorBaseAllocationFree` + `AbpInvocationStruct`의 `protected Internal*` |
| 내장 aspect 메타데이터 | 런타임 `MethodInfo` 리플렉션 | compile-time: `AbpMethodInterceptionMetadata`로 bake, `AbpMethodInfo.TryGetMetadata(method, out metadata)`로 조회 |
| 헬퍼 등록 | `ITransientDependency` convention | 동일; Castle registrar는 인터셉터 **프록시**만 등록 |

`AbpInvocationExtensions.GetMethodInvocationTarget` / `GetAbpMethod`는 `invocation.MethodInvocationTarget`을 반환합니다.

**프레임워크 헬퍼** (`AuthorizationHelper`, `AuditingHelper`, `MethodInvocationValidator`, `EntityHistoryUseCaseDescriptionProvider`)는 `Abp`에 있으며 기존 `MethodInfo` 기반 인터페이스를 구현합니다. 각각 `AbpMethodInfo.TryGetMetadata`를 먼저 확인하고, bake된 메타데이터가 없으면 기존 리플렉션 경로로 폴백합니다 (Castle 호환).

**메타데이터 모델** (`Abp.Dependency`):

- `AbpMethodInterceptionMetadata` — bake된 필드 (`ShouldAudit`, `ShouldValidate`, `UnitOfWorkAttribute`, `AuthorizeAttributes`, …)
- `AbpMethodInterceptionMetadataProvider.Instance` — 생성된 `{Name}_Intercepted` 정적 생성자가 채우는 런타임 레지스트리
- `AbpMethodInfo` — `MethodInfo` 서브클래스; `GetInvocationMethod` / `TryGetMetadata`로 Castle·compile-time 경로 통합

생성 코드는 `nameof(Type.Method)`로 메서드 이름을 해석하여 리팩터링에 안전합니다.

## Compile-time 속성 (`Abp.Dependency.CompileTime`, `Abp.SourceGenerators.Runtime`에 정의)

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

### 제거·중단해도 되는 것

- 애플리케이션 서비스용 `AbpAsyncDeterminationInterceptor<T>` 수동 등록 (compile-time은 생성된 데코레이터 사용).
- 애플리케이션 코드에서 Castle `IInvocation` 의존.
- compile-time 인터셉션 활성화 후 `IApplicationService`에 DynamicProxy가 붙을 것이라는 가정.

### 변하지 않는 것

- 모듈 구조, `DependsOn`, `PreInitialize` / `PostInitialize`.
- 애플리케이션 서비스 인터페이스와 DTO.
- 내장 인터셉터 **클래스** (`AuthorizationInterceptor`, `UnitOfWorkInterceptor` 등) — IoC에서 여전히 resolve; **연결 방식**만 변경.
- IoC 컨테이너로서의 Castle.Windsor (2단계까지).

## 4. 생성물

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

## compile-time 실행 경로

생성된 메서드는 스택 `AbpInvocationStruct` / `AbpInvocationStruct<TAsync>`와 `CompileTimeInvocationInterceptorExecutor`를 통해 실행됩니다. 내장 인터셉터(`AuthorizationInterceptor`, `AuditingInterceptor` 등)는 `AbpInterceptorBase`만 상속하며 항상 **class-bridge** 경로(`AbpInvocationCompileTime` / `IAbpInvocation`)를 사용합니다.

사용자 인터셉터는 레이어별로 다음처럼 라우팅됩니다.

| 반환 형태 | class-bridge (`AbpInterceptorBase`) | allocation-free (`AbpInterceptorBaseAllocationFree`) |
|:----------|:-----------------------------------|:-------------------------------------------------------|
| sync | `RunSyncLayer` → `InterceptSynchronous(IAbpInvocation)` | `RunSyncAllocationFreeLayer` → `IAbpInterceptorSync` → `protected InternalInterceptSynchronous(ref AbpInvocationStruct)` |
| `Task` / `Task<T>` | `RunTaskViaClassBridgeLayer` → `InternalInterceptAsynchronous(IAbpInvocation)` | `RunTaskAllocationFreeLayer` → `IAbpInterceptorTaskAsync` → `protected InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>>)` (by value) |
| `ValueTask` / `ValueTask<T>` | `RunValueTaskViaClassBridgeLayer` (Task 호환 브리지) | `RunValueTaskAllocationFreeLayer`, 또는 Task struct `Internal*`만 있을 때 `RunValueTaskViaTaskStructLayer` |

`AbpInvocationCompileTimeAsyncBridge`는 내장·사용자 인터셉터가 한 체인에 섞일 때 struct 레이어와 class-bridge 인터셉터를 연결합니다. allocation-free 인터셉터에는 compile-time 경로에서 `IAbpInterceptorSync` / `IAbpInterceptorTaskAsync` / `IAbpInterceptorValueTaskAsync`를 직접 호출합니다 (`IAbpInvocation` 미사용).

생성기는 `AbpInterceptorBaseAllocationFree`에서 proceed-only가 아닌 `protected Internal*` struct override가 있으면 allocation-free 라우팅을 선택합니다 (`AllocationFreeInterceptorAnalyzer`).

## 5. 사용자 정의 인터셉터

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

**allocation-free** (선택; 사용자 인터셉터 레이어에서 heap 할당 감소). `AbpInterceptorBaseAllocationFree`를 상속하고 `protected Internal*` struct 메서드만 override합니다. public `InterceptSynchronous` / `InterceptAsynchronous` struct 인터페이스 메서드는 override하지 않습니다. 참고:

- **fast path (권장):** [`StructFastPathCompileTimeInterceptor.cs`](../test/Abp.Interception.CompileTime.Host/Interceptors/StructFastPathCompileTimeInterceptor.cs) — sync `ref` + `Proceed()`, async `return await invocation.Proceed()`.
- **기존 코드 호환:** [`StructCompatCompileTimeInterceptor.cs`](../test/Abp.Interception.CompileTime.Host/Interceptors/StructCompatCompileTimeInterceptor.cs) — async `CaptureProceedInfo()` → 선행 작업 → `proceedInfo.Invoke()` (`IAbpInvocation` 인터셉터와 동일 형태).

```csharp
// fast path
[AbpInterceptor(typeof(StructFastPathTaggedAttribute))]
public sealed class StructFastPathCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
    {
        /* 선행 작업 */ invocation.Proceed();
    }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(
        AbpInvocationStruct<Task<TResult>> invocation)
    {
        /* 선행 작업 */ return await invocation.Proceed().ConfigureAwait(false);
    }
}

// IAbpInvocation 호환 (기존 async 인터셉터 포팅)
[AbpInterceptor(typeof(StructCompatTaggedAttribute))]
public sealed class StructCompatCompileTimeInterceptor : AbpInterceptorBaseAllocationFree, ITransientDependency
{
    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(
        AbpInvocationStruct<Task<TResult>> invocation)
    {
        var proceedInfo = invocation.CaptureProceedInfo();
        /* 선행 작업 (await 가능) */
        return await proceedInfo.Invoke().ConfigureAwait(false);
    }
}
```

동기는 `ref AbpInvocationStruct`로 `ReturnValue`를 제자리에서 갱신합니다. async struct 파라미터는 **by value** (proceed delegate 공유, `async override` 지원). void `Task` / `ValueTask` 메서드는 내부적으로 `AbpUnit` 결과 타입을 사용합니다 (emitter의 `AbpAsyncCoercion`).

`AbpInvocationStruct<TAsync>.CaptureProceedInfo()`는 선택 사항 — proceed delegate가 바뀌지 않으면 `Proceed()`와 동일합니다. `IAbpInvocation` 인터셉터를 줄 단위로 옮길 때만 사용합니다.

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

## 6. NativeAOT 게시 (선택)

샘플 프로젝트 참고: `<PublishAot>true</PublishAot>`, `ILLink.Descriptors.xml`, `EmitCompilerGeneratedFiles`로 `obj/Generated/Abp.SourceGenerators/`에서 생성 코드 확인.

## 7. 아직 Castle에 남는 부분

`Abp.Web.Common`은 기본 스택을 위해 `Abp.Interception.Castle`을 참조합니다. compile-time 인터셉션은 `CompileTimeInterceptionConfiguration.Enable()`로 **명시적으로 켠 경우** 등록된 서비스의 DynamicProxy만 대체합니다. IoC는 로드맵 2단계까지 Castle.Windsor를 유지합니다.

## 8. 검증

[`test/Abp.Interception.CompileTime.Tests`](../test/Abp.Interception.CompileTime.Tests) 실행 — [`test/Abp.Interception.CompileTime.Host`](../test/Abp.Interception.CompileTime.Host) 대상 통합 테스트:

| 테스트 클래스 | 검증 내용 |
|:--------------|:----------|
| `TaggedCompileTimeInterceptorWebTests` | class-bridge 사용자 인터셉터 (`AbpInterceptorBase` + `IAbpInvocation`) |
| `StructAllocationFreeInterceptorWebTests` | allocation-free struct: `StructFastPathCompileTimeInterceptor` (fast `Proceed`) 및 `StructCompatCompileTimeInterceptor` (`CaptureProceedInfo` 호환) |
| `BuiltInInterceptorWebTests` | 내장 auditing과 사용자 인터셉터가 같은 체인에서 동작 |

호스트 프로젝트에 `EmitCompilerGeneratedFiles`가 켜져 있으면 생성 코드는 `obj/Generated/Abp.SourceGenerators/`에서 볼 수 있습니다.
