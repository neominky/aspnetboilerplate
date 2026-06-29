using Abp.Dependency.CompileTime;
using Xunit;

namespace Abp.Interception.CompileTime.Tests;

public class CompileTimeInvocationScopeTests
{
    [Fact]
    public void Nested_scopes_should_restore_parent_scope_on_dispose()
    {
        using var outer = CompileTimeInvocationScope.Begin();
        outer.ClassInvocation = new AbpInvocationCompileTime(
            new object(),
            typeof(CompileTimeInvocationScopeTests).GetMethod(nameof(Nested_scopes_should_restore_parent_scope_on_dispose))!,
            []);

        using (var inner = CompileTimeInvocationScope.Begin())
        {
            Assert.NotSame(outer, CompileTimeInvocationScope.Active);
            Assert.Same(inner, CompileTimeInvocationScope.Active);
        }

        Assert.Same(outer, CompileTimeInvocationScope.Active);
        Assert.Same(outer.ClassInvocation, CompileTimeInvocationScope.Active!.ClassInvocation);
    }

    [Fact]
    public void Dispose_should_only_restore_when_disposing_active_scope()
    {
        using var outer = CompileTimeInvocationScope.Begin();
        var inner = CompileTimeInvocationScope.Begin();

        inner.Dispose();
        Assert.Same(outer, CompileTimeInvocationScope.Active);

        outer.Dispose();
        Assert.Null(CompileTimeInvocationScope.Active);
    }
}
