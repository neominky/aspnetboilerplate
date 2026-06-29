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
    options.InterceptorOptions.DisableAuditingInterceptor = true; // optional
    CompileTimeInterceptionConfiguration.Enable(options.InterceptorOptions);
});
```

또는 `AbpBootstrapper.CreateWithCompileTimeInterception<TModule>()`를 사용합니다. 이 확장은 `configure` 실행 후 `options.InterceptorOptions`를 compile-time 경로에 전달합니다.

`CompileTimeInterceptionConfiguration.Enable()`은 Castle validation/auditing/unit of work/authorization/entity history 등록기를 비활성화합니다. compile-time으로 생성된 `{Service}_Intercepted`는 여전히 attribute 기준으로 빌트인 레이어를 bake하지만, `CompileTimeBuiltInInterceptorProvider`가 startup `InterceptorOptions`를 읽어 비활성화된 빌트인은 `CompileTimeNoOpInterceptor`로 대체합니다(Castle의 registrar 비활성화와 동일한 효과).

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
| 시작 | `services.AddAbp<MyModule>()` | `AddAbp` 옵션에서 `CompileTimeInterceptionConfiguration.Enable(options.InterceptorOptions)` 호출 (`Initialize` 이전) |
| 모듈 클래스 | `public class MyModule` | `public partial class MyModule` |
| Convention 등록 | `IocManager.RegisterAssemblyByConvention(typeof(MyModule).Assembly)` | `using Abp.Dependency.CompileTime;` 후 `IocManager.RegisterAssemblyByConvention(typeof(MyModule))` |
| 앱 서비스 resolve | 구현 타입 + Castle 프록시 | IoC가 생성된 `{Service}_Intercepted` 데코레이터 resolve |
| 사용자 인터셉터 | Castle 전용 wiring | 애플리케이션 서비스와 **같은 어셈블리**의 `AbpInterceptorBase` + `ITransientDependency` + `[AbpInterceptor(typeof(TriggerAttribute))]` (또는 `[AbpIntercept]`) |

`IApplicationService` 구현 클래스는 대부분 **코드 변경 없이** 동작합니다. `[AbpAuthorize]`, `[Audited]`, `[UnitOfWork]`, `[DisableValidation]`, `[UseCase]` 등 내장 aspect는 compile-time에 분석되어 `AbpMethodInterceptionMetadata`로 bake되며, 생성된 `{Name}_Intercepted` 정적 생성자가 `AbpMethodInterceptionMetadataProvider`에 등록합니다.

## 프로젝트 역할

| 프로젝트 | 역할 |
|:---------|:-----|
| `Abp.SourceGenerators` | Roslyn 소스 생성기. `{Service}_Intercepted`, 모듈 IoC partial, 메타데이터 등록 코드 생성. |
| `Abp.SourceGenerators.Runtime` | `Abp.Dependency.CompileTime` 네임스페이스의 런타임 지원 (`CompileTimeInterceptionConfiguration`, `AbpInvocationCompileTime`, `AbpInvocationStruct`, `AbpInterceptorBaseAllocationFree`, `IAbpInterceptorSync` / Task / ValueTask async 인터페이스, `CompileTimeInvocationInterceptorExecutor`, `AbpSyncChainStepDelegate` / `AbpAsyncChainStepDelegate`, IoC 확장). |
| `Abp` | 공유 런타임 모델: `AbpMethodInfo`, `AbpMethodInterceptionMetadata`, `AbpMethodInterceptionMetadataProvider`, `IAbpInvocation`, 메타데이터 fast path를 가진 프레임워크 헬퍼. |
| `Abp.Interception.Castle` | compile-time 인터셉션이 **비활성**일 때의 기본 Castle DynamicProxy 경로. |

`Abp.SourceGenerators.Runtime` 폴더 구조는 `Abp`와 대응됩니다: `Dependency/CompileTime/`, `Runtime/Validation/Interception/`, 프로젝트 루트의 `AbpBootstrapperCompileTimeExtensions.cs`.

## API 및 추상화 변경

Castle 리플렉션과 compile-time 메타데이터가 동일한 런타임 API를 공유합니다. **class invocation 사용자 인터셉터는 `IAbpInvocation`을, allocation-free 사용자 인터셉터는 스택 `AbpInvocationStruct`의 `protected Internal*`를 override합니다.** 프레임워크 헬퍼는 기존 `MethodInfo` 기반 인터페이스를 유지하며 bake된 메타데이터가 있으면 우선 사용합니다.

| 영역 | 이전 | 이후 |
|:-----|:-----|:-----|
| Invocation | `Castle.DynamicProxy.IInvocation` | `IAbpInvocation` (`Abp.Dependency`) |
| 인터셉터의 메서드 | `invocation.Method` | `invocation.MethodInvocationTarget` (메타데이터가 있으면 `AbpMethodInfo`일 수 있음) |
| 비동기 proceed | Castle `Proceed()` | **class invocation:** `invocation.CaptureProceedInfo().Invoke()` 후 `invocation.ReturnValue`를 `Task` / `Task<T>`로 await. **allocation-free fast path:** `return await invocation.Proceed()` (struct by value). **allocation-free 호환:** `CaptureProceedInfo()` → 작업 → `proceedInfo.Invoke()` (선택; delegate가 같으면 `Proceed()`와 동일). |
| 인터셉터 베이스 | `AbpInterceptorBase` | 동일; class invocation override는 `IAbpInvocation`. allocation-free: `AbpInterceptorBaseAllocationFree` + `AbpInvocationStruct`의 `protected Internal*` |
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

## compile-time 실행 모델

생성기는 bake된 인터셉터 레이어 종류(`AllocationFreeInterceptorAnalyzer`)에 따라 메서드마다 **세 가지 경로** 중 하나를 선택합니다. 생성된 `{Name}_Intercepted` 코드는 class invocation 레이어에서 `CompileTimeInvocationInterceptorExecutor`를 호출하고, allocation-free 레이어는 struct 인터페이스를 직접 호출합니다.

**용어:** `IAbpInvocation` / `AbpInvocationCompileTime` 경로는 **class invocation**입니다. 예전 struct↔class “bridge” 어댑터(`EnsureClassBridge`, async bridge 타입)는 제거되었습니다. 대응 개념은 **struct invocation**(`AbpInvocationStruct`, allocation-free 인터셉터)입니다.

### 경로 선택

| 경로 | 조건 | invocation 상태 | 체인 디스패치 |
|:-----|:-----|:----------------|:--------------|
| **Pure class invocation (sync)** | 모든 레이어가 `ClassInvocation` | `CompileTimeInvocationPool`에서 `AbpInvocationCompileTime` rent → layer에 **인자로 전달** → `finally`에서 return | `{Method}_SyncClassLayer0(classInvocation)` 직접 호출 |
| **Pure class invocation (async)** | 동일 | `CompileTimeInvocationScope`(`AsyncLocal`, **중첩 scope 스택**) + pooled `AbpInvocationCompileTime` | scope.ClassInvocation + layer 메서드 |
| **Pure allocation-free** | 모든 레이어가 allocation-free | 스택 `AbpInvocationStruct` 로컬 | 체인 배열 + `Proceed()` |
| **Mixed (sync)** | class + allocation-free 공존 | pooled `AbpInvocationStructHolder` + pooled `AbpInvocationCompileTime`; struct에 `ClassInvocation` 연결 | struct 체인; class proceed는 `classInvocation.MixedStructHolder!.Value.Proceed()` |
| **Mixed (async)** | 동일 | `CompileTimeInvocationScope` + `AsyncStructHolder<TAsync>` + pooled class invocation | async holder `Value.ClassInvocation`로 mixed class layer 연결 |

**스레드 안전성 (하이브리드):**
- **sync pure class / mixed sync:** `AsyncLocal` 없음. 로컬 변수·인자 전달 + (mixed) `AbpInvocationStructHolder`로 struct 상태를 heap box에 두어 class proceed가 동일 struct를 진행.
- **async class / mixed async:** `CompileTimeInvocationScope` + `AsyncLocal`. `Begin()`/`Dispose()`가 **parent scope를 복원**해 인터셉트된 메서드의 중첩 호출(reentrancy)도 안전.
- **allocation-free:** 스택 struct (기존과 동일).

**Object pool:** `CompileTimeInvocationPool`이 `AbpInvocationCompileTime`과 `AbpInvocationStructHolder`를 재사용. warmup 이후 sync class invocation은 **0 B** 할당(벤치마크 기준).

### Baked proceed delegate (호출마다 lambda 할당 없음)

class invocation 레이어는 `Func<object?>`(sync) 또는 `Func<Task<object?>>`(async) proceed가 필요합니다. 생성기는 다음을 emit합니다.

| 생성물 | 역할 |
|--------|------|
| `{Method}_SyncClassProceedN` / `{Method}_TaskClassProceedN` / `{Method}_ValueTaskClassProceedN` | private proceed 메서드; ctor에서 `readonly Func<…>` 필드로 method group 연결 |
| **Pure class invocation** proceed | 다음 `{Method}_SyncClassLayerK` 또는 `{Method}_SyncClassTail` 호출 |
| **Mixed** proceed | `classInvocation.MixedStructHolder!.Value.Proceed()` (sync) 또는 async holder `Value.Proceed()` |

async proceed는 `Task.FromResult<object?>(task)`를 반환합니다. 여기서 `task`는 **await 결과가 아닌 `Task<TResult>` 객체**입니다. 내장 인터셉터는 `CaptureProceedInfo().Invoke()` 후 `invocation.ReturnValue`를 `Task` / `Task<T>`로 읽습니다.

### Pure class invocation sync (예시)

```csharp
// sync 진입 (pool + 인자 전달, AsyncLocal 없음)
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

### Pure allocation-free sync (예시)

```csharp
AbpInvocationStruct syncInvocation = default;
syncInvocation.Initialize(_inner, GetMessageInvocationMethod, arguments);
syncInvocation.BeginSyncChain(_getMessageSyncChain);
syncInvocation.Proceed();
return (string)syncInvocation.ReturnValue!;
```

### Mixed sync (예시)

내장 class invocation 레이어가 체인 앞쪽, allocation-free 사용자 인터셉터가 뒤쪽. class proceed가 struct 체인을 진행합니다:

```csharp
private object? GetMessage_SyncClassProceed0()
{
    _getMessageSyncInvocationSlot.Proceed();
    return _getMessageSyncInvocationSlot.ReturnValue;
}
```

### 비동기 (Task / ValueTask)

동일한 세 경로. pure class invocation 진입:

```csharp
_getMessageTaskAsyncTaskClassInvocation.PrepareForCall(arguments);
return GetMessageTaskAsync_TaskClassLayer0();
```

`RunTaskClassLayer` / `RunValueTaskClassLayer`(mixed는 `ref` 오버로드)가 `ResolveTaskReturn` / `ResolveValueTaskReturn`으로 위임합니다. ValueTask proceed는 `ValueTask<TResult>`를 `Task.FromResult<object?>(…)`로 감쌀 때 `.AsTask()`를 사용합니다.

### 인터셉터 라우팅 (의미는 동일)

내장 인터셉터(`AuthorizationInterceptor`, `AuditingInterceptor` 등)는 `AbpInterceptorBase`만 상속하며 항상 **class invocation** 경로를 사용합니다.

| 반환 형태 | class invocation (`AbpInterceptorBase`) | allocation-free (`AbpInterceptorBaseAllocationFree`) |
|:----------|:-----------------------------------|:-------------------------------------------------------|
| sync | `AbpInvocationCompileTime` 경유 `InterceptSynchronous(IAbpInvocation)` | `IAbpInterceptorSync.InterceptSynchronous(ref AbpInvocationStruct)` |
| `Task` / `Task<T>` | `AbpInvocationCompileTime`에서 `InterceptAsynchronous` / `InterceptAsynchronous<T>` | `IAbpInterceptorTaskAsync.InterceptAsynchronous<T>(invocation)` |
| `ValueTask` / `ValueTask<T>` | `AbpInvocationCompileTime` (executor 내부 Task materialization) | `IAbpInterceptorValueTaskAsync`, 또는 Task `Internal*`만 있을 때 `RunValueTaskTaskStructLayer` |

`AllocationFreeInterceptorAnalyzer`가 `AbpInterceptorBaseAllocationFree`의 비 trivial `protected Internal*` override를 찾으면 allocation-free 라우팅을 선택합니다.

### 캐싱·재사용

- `AbpMethodInfo.GetInvocationMethod` — invocation `MethodInfo` 래퍼용 `ConcurrentDictionary` 캐시.
- `{Name}_Intercepted`의 static `AbpInvocationMethod` 필드 — 타입 로드 시 메서드당 1회.
- `AbpInvocationCompileTime` — **인터셉트된 메서드당 인스턴스 1개**(sync / Task / ValueTask), `PrepareForCall`로 호출마다 재사용.
- Proceed `Func` 필드 — ctor에서 method group으로 1회 연결 (호출마다 delegate 할당 없음).

## 벤치마크 (fork)

[`benchmark/`](../benchmark/) 프로젝트:

| 프로젝트 | 역할 |
|---------|------|
| `Abp.Interception.Benchmarks.Contracts` | 공통 카운터·검증 |
| `Abp.Interception.Benchmarks.NuGet` | NuGet Abp 10.4 + Castle DynamicProxy |
| `Abp.Interception.Benchmarks.Fork` | 본 fork + `Abp.SourceGenerators` (class invocation vs allocation-free) |
| `Abp.Interception.Benchmarks.RunAll` | NuGet·Fork를 **별도 프로세스**로 실행 (동일 `Abp` 어셈블리 이중 로드 불가) |

실행 (Release):

```bash
dotnet run -c Release --project benchmark/Abp.Interception.Benchmarks.NuGet -- --filter "*"
dotnet run -c Release --project benchmark/Abp.Interception.Benchmarks.Fork -- --filter "*"
dotnet run -c Release --project benchmark/Abp.Interception.Benchmarks.Fork -- --filter "*" --verify
```

시나리오당 **사용자 인터셉터 3개**, sync/async 호출 1회. 샘플 결과 (.NET 9, i9-14900KF, Release, 스레드 안전 `CompileTimeInvocationScope` 반영 후 재측정):

| 시나리오 | NuGet Castle | Fork class invocation | Fork allocation-free |
|----------|-------------:|------------------------:|---------------------:|
| Sync | 40.9 ns / 104 B | **~69 ns / 0 B** | **~15 ns / 0 B** |
| Task async | 962.5 ns / 806 B | ~1,039 ns / 979 B | ~911 ns / 749 B |

Fork 전체 BenchmarkDotNet 결과 (동일 환경, pool warmup 후):

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Compile-time (class invocation) | 68.95 ns | **0 B** |
| Compile-time (allocation-free) | 15.13 ns | 0 B |
| Compile-time (class invocation) Task | 1,039.12 ns | 979 B |
| Compile-time (allocation-free) Task | 911.01 ns | 749 B |

sync class invocation은 pool 덕분에 **0 B**이며 Castle보다 약간 느리지만 스레드 안전합니다. async class invocation은 `CompileTimeInvocationScope` 비용으로 ~1 KB/call. allocation-free가 sync·async 모두 Castle 대비 우위.

## compile-time 실행 경로 (`CompileTimeInvocationInterceptorExecutor`)

생성된 `{Name}_Intercepted`는 모든 class invocation 레이어에서 `CompileTimeInvocationInterceptorExecutor`를 호출합니다. allocation-free 레이어는 struct 인터셉터 인터페이스를 직접 호출합니다.

| 반환 형태 | pure class invocation | mixed class invocation | allocation-free |
|:----------|:------------------|:-------------------|:----------------|
| sync | `RunSyncClassLayer(invocation, interceptor, proceed)` | `RunSyncClassLayer(ref inv, interceptor, invocation, proceed)` | `RunSyncAllocationFreeLayer(ref inv, interceptor)` |
| `Task` / `Task<T>` | `RunTaskClassLayer<T>(invocation, interceptor, proceed)` | `RunTaskClassLayer<T>(ref inv, interceptor, invocation, proceed)` | `RunTaskAllocationFreeLayer<T>(ref inv, interceptor)` |
| `ValueTask` / `ValueTask<T>` | `RunValueTaskClassLayer<T>(invocation, interceptor, proceed)` | `RunValueTaskClassLayer<T>(ref inv, interceptor, invocation, proceed)` | `RunValueTaskAllocationFreeLayer<T>` / `RunValueTaskTaskStructLayer<T>` |

`ResolveTaskReturn` / `ResolveValueTaskReturn`은 인터셉터 실행 후 `AbpInvocationCompileTime.ReturnValue`를 정규화합니다.

**제거됨** (더 이상 emit·참조하지 않음): `EnsureClassBridge`, `SyncInvocationState`, `AbpInvocationCompileTimeAsyncBridge`, `AbpInvocationCompileTimeTaskCompatible`, 중첩 `RunTaskViaClassBridgeLayer` lambda.

내장 인터셉터는 항상 class invocation. 사용자 인터셉터 라우팅은 위 **compile-time 실행 모델** 표를 참고하세요.

## 5. 사용자 정의 인터셉터

**class invocation** (기본 권장; 내장 인터셉터와 동일 패턴). `test/Abp.Interception.CompileTime.Host/Interceptors/TaggedCompileTimeInterceptor.cs` 참고:

```csharp
[AbpInterceptor(typeof(TaggedAttribute))]
public sealed class TaggedCompileTimeInterceptor : AbpInterceptorBase, ITransientDependency
{
    public override void InterceptSynchronous(IAbpInvocation invocation) { /* ... */ }

    protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation) { /* ... */ }

    protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation) { /* ... */ }
}
```

`ValueTask` 반환 애플리케이션 메서드도 동일한 `InternalInterceptAsynchronous` / `InternalInterceptAsynchronous<TResult>`로 진입하며, 필요 시 `CompileTimeInvocationInterceptorExecutor`가 class invocation 레이어에서 `ValueTask`를 `Task`로 materialize합니다.

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
| `TaggedCompileTimeInterceptorWebTests` | class invocation 사용자 인터셉터 (`AbpInterceptorBase` + `IAbpInvocation`) |
| `StructAllocationFreeInterceptorWebTests` | allocation-free struct: `StructFastPathCompileTimeInterceptor` (fast `Proceed`) 및 `StructCompatCompileTimeInterceptor` (`CaptureProceedInfo` 호환) |
| `BuiltInInterceptorWebTests` | 내장 auditing과 사용자 인터셉터가 같은 체인에서 동작 |

호스트 프로젝트에 `EmitCompilerGeneratedFiles`가 켜져 있으면 생성 코드는 `obj/Generated/Abp.SourceGenerators/`에서 볼 수 있습니다.

벤치마크 검증 (호출당 인터셉터 3개):

```bash
dotnet run -c Release --project benchmark/Abp.Interception.Benchmarks.Fork -- --filter "*" --verify
```
