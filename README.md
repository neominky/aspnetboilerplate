# ASP.NET Boilerplate

[![Build Status](https://github.com/aspnetboilerplate/aspnetboilerplate/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/aspnetboilerplate/aspnetboilerplate/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/v/Abp.svg?style=flat-square)](https://www.nuget.org/packages/Abp)
[![MyGet (with prereleases)](https://img.shields.io/myget/abp-nightly/vpre/Abp.svg?style=flat-square)](https://aspnetboilerplate.com/Pages/Documents/Nightly-Builds)
[![NuGet Download](https://img.shields.io/nuget/dt/Abp.svg?style=flat-square)](https://www.nuget.org/packages/Abp)

## Fork Purpose

This repository is a fork of [ASP.NET Boilerplate](https://github.com/aspnetboilerplate/aspnetboilerplate). It is maintained and evolved with goals that differ from the upstream project.

Fork-specific documentation lives in [`fork-doc/`](fork-doc/). Upstream docs and images remain in [`doc/`](doc/).

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

Replace Castle `DynamicProxy` service interception with compile-time source generation (`Abp.SourceGenerators`). Castle and compile-time paths are **mutually exclusive**.

**Quick steps**

1. Reference `Abp.SourceGenerators.Runtime` and `Abp.SourceGenerators` (analyzer).
2. Call `CompileTimeInterceptionConfiguration.Enable()` before module `Initialize`.
3. Declare `public partial class MyModule` and call `IocManager.RegisterAssemblyByConvention(typeof(MyModule))` from `Abp.Dependency.CompileTime`.

Sample: [`test/Abp.Interception.CompileTime.Host`](test/Abp.Interception.CompileTime.Host) · Tests: [`test/Abp.Interception.CompileTime.Tests`](test/Abp.Interception.CompileTime.Tests)

**Detailed guide:** [Compile-Time Interception Migration](fork-doc/compile-time-interception-migration.md) · [한국어](fork-doc/compile-time-interception-migration.ko.md)

### 한국어

이 저장소는 [ASP.NET Boilerplate](https://github.com/aspnetboilerplate/aspnetboilerplate)의 포크이며, 원본 프로젝트의 방향과는 별도의 목표를 가지고 유지·발전시킵니다.

포크 전용 문서는 [`fork-doc/`](fork-doc/)에 있습니다. upstream 문서·이미지는 [`doc/`](doc/)에 그대로 둡니다.

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

Castle `DynamicProxy` 서비스 인터셉션을 compile-time 소스 생성(`Abp.SourceGenerators`)으로 대체합니다. Castle 경로와 compile-time 경로는 **동시에 사용하지 않습니다**.

**요약 절차**

1. `Abp.SourceGenerators.Runtime`과 `Abp.SourceGenerators`(analyzer) 참조 추가
2. 모듈 `Initialize` 이전에 `CompileTimeInterceptionConfiguration.Enable()` 호출
3. `public partial class MyModule` 선언 후 `Abp.Dependency.CompileTime`의 `IocManager.RegisterAssemblyByConvention(typeof(MyModule))` 사용

샘플: [`test/Abp.Interception.CompileTime.Host`](test/Abp.Interception.CompileTime.Host) · 테스트: [`test/Abp.Interception.CompileTime.Tests`](test/Abp.Interception.CompileTime.Tests)

**상세 가이드:** [Compile-Time Interception Migration](fork-doc/compile-time-interception-migration.md) · [한국어](fork-doc/compile-time-interception-migration.ko.md)

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
