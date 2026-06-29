using Abp.Dependency;
using Abp.Dependency.CompileTime;
using Xunit;

namespace Abp.Interception.CompileTime.Tests;

public class CompileTimeBuiltInInterceptorOptionsTests
{
    [Fact]
    public void Provider_should_return_noop_when_auditing_interceptor_disabled()
    {
        var previous = SnapshotInterceptorOptions();
        try
        {
            CompileTimeInterceptionConfiguration.ConfigureInterceptorOptions(new AbpBootstrapperInterceptorOptions
            {
                DisableAuditingInterceptor = true,
            });

            var resolver = new ThrowingIocResolver();
            var interceptor = CompileTimeBuiltInInterceptorProvider.Auditing(resolver);

            Assert.Same(CompileTimeNoOpInterceptor.Instance, interceptor);
        }
        finally
        {
            RestoreInterceptorOptions(previous);
        }
    }

    [Fact]
    public void Provider_should_resolve_validation_when_not_disabled()
    {
        var previous = SnapshotInterceptorOptions();
        try
        {
            CompileTimeInterceptionConfiguration.ConfigureInterceptorOptions(new AbpBootstrapperInterceptorOptions
            {
                DisableValidationInterceptor = false,
            });

            var resolver = new FakeIocResolver();
            var interceptor = CompileTimeBuiltInInterceptorProvider.Validation(resolver);

            Assert.Same(resolver.ValidationInterceptor, interceptor);
        }
        finally
        {
            RestoreInterceptorOptions(previous);
        }
    }

    [Fact]
    public void Noop_interceptor_should_forward_sync_proceed()
    {
        var proceeded = false;
        var invocation = new AbpInvocationCompileTime(
            new object(),
            typeof(CompileTimeBuiltInInterceptorOptionsTests).GetMethod(nameof(Noop_interceptor_should_forward_sync_proceed))!,
            []);
        invocation.SetSyncProceed(_ =>
        {
            proceeded = true;
            return null;
        });

        CompileTimeNoOpInterceptor.Instance.InterceptSynchronous(invocation);

        Assert.True(proceeded);
    }

    private static AbpBootstrapperInterceptorOptions SnapshotInterceptorOptions()
    {
        var current = CompileTimeInterceptionConfiguration.InterceptorOptions;
        return new AbpBootstrapperInterceptorOptions
        {
            DisableValidationInterceptor = current.DisableValidationInterceptor,
            DisableAuditingInterceptor = current.DisableAuditingInterceptor,
            DisableEntityHistoryInterceptor = current.DisableEntityHistoryInterceptor,
            DisableUnitOfWorkInterceptor = current.DisableUnitOfWorkInterceptor,
            DisableAuthorizationInterceptor = current.DisableAuthorizationInterceptor,
        };
    }

    private static void RestoreInterceptorOptions(AbpBootstrapperInterceptorOptions snapshot)
    {
        CompileTimeInterceptionConfiguration.ConfigureInterceptorOptions(snapshot);
    }

    private sealed class ThrowingIocResolver : IIocResolver
    {
        public object Resolve(Type type) => throw new InvalidOperationException($"Resolve should not be called for {type.Name}.");

        public T Resolve<T>() => throw new InvalidOperationException($"Resolve should not be called for {typeof(T).Name}.");

        public T Resolve<T>(Type type) => throw new InvalidOperationException($"Resolve should not be called for {type.Name}.");

        public object Resolve(Type type, object argumentsAsAnonymousType) => Resolve(type);

        public T Resolve<T>(object argumentsAsAnonymousType) => Resolve<T>();

        public object[] ResolveAll(Type type) => throw new NotSupportedException();

        public object[] ResolveAll(Type type, object argumentsAsAnonymousType) => throw new NotSupportedException();

        public T[] ResolveAll<T>() => throw new NotSupportedException();

        public T[] ResolveAll<T>(object argumentsAsAnonymousType) => throw new NotSupportedException();

        public bool IsRegistered(Type type) => false;

        public bool IsRegistered<T>() => false;

        public void Release(object obj)
        {
        }
    }

    private sealed class FakeIocResolver : IIocResolver
    {
        public Abp.Runtime.Validation.Interception.ValidationInterceptor ValidationInterceptor { get; } = new(null!);

        public object Resolve(Type type)
        {
            if (type == typeof(Abp.Runtime.Validation.Interception.ValidationInterceptor))
            {
                return ValidationInterceptor;
            }

            throw new NotSupportedException(type.FullName);
        }

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public T Resolve<T>(Type type) => (T)Resolve(type);

        public object Resolve(Type type, object argumentsAsAnonymousType) => Resolve(type);

        public T Resolve<T>(object argumentsAsAnonymousType) => Resolve<T>();

        public object[] ResolveAll(Type type) => throw new NotSupportedException();

        public object[] ResolveAll(Type type, object argumentsAsAnonymousType) => throw new NotSupportedException();

        public T[] ResolveAll<T>() => throw new NotSupportedException();

        public T[] ResolveAll<T>(object argumentsAsAnonymousType) => throw new NotSupportedException();

        public bool IsRegistered(Type type) => false;

        public bool IsRegistered<T>() => false;

        public void Release(object obj)
        {
        }
    }
}
