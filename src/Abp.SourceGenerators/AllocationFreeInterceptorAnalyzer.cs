using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Abp.SourceGenerators;

internal static class AllocationFreeInterceptorAnalyzer
{
    public static SyncLayerKind ResolveSyncLayerKind(
        INamedTypeSymbol interceptorType,
        INamedTypeSymbol? allocationFreeBase,
        Compilation compilation)
    {
        if (allocationFreeBase == null || !InheritsFrom(interceptorType, allocationFreeBase))
        {
            return SyncLayerKind.ClassBridge;
        }

        var invocationStruct = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.AbpInvocationStruct);
        if (invocationStruct == null)
        {
            return SyncLayerKind.ClassBridge;
        }

        if (OverridesStructSyncIntercept(interceptorType, allocationFreeBase, invocationStruct))
        {
            return SyncLayerKind.AllocationFreeSync;
        }

        return SyncLayerKind.ClassBridge;
    }

    public static ValueTaskLayerKind ResolveValueTaskLayerKind(
        INamedTypeSymbol interceptorType,
        INamedTypeSymbol? allocationFreeBase,
        Compilation compilation)
    {
        if (allocationFreeBase == null || !InheritsFrom(interceptorType, allocationFreeBase))
        {
            return ValueTaskLayerKind.ClassBridge;
        }

        var invocationStruct = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.AbpInvocationStructOpen);
        var valueTask = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.ValueTask);
        var valueTaskGeneric = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.ValueTaskOpen);
        var task = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.Task);
        var taskGeneric = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.TaskOpen);

        if (invocationStruct == null || valueTask == null || valueTaskGeneric == null || task == null || taskGeneric == null)
        {
            return ValueTaskLayerKind.ClassBridge;
        }

        if (OverridesStructIntercept(interceptorType, allocationFreeBase, invocationStruct, valueTask, isGeneric: false)
            || OverridesStructIntercept(interceptorType, allocationFreeBase, invocationStruct, valueTaskGeneric, isGeneric: true))
        {
            return ValueTaskLayerKind.AllocationFreeValueTask;
        }

        if (OverridesStructIntercept(interceptorType, allocationFreeBase, invocationStruct, task, isGeneric: false)
            || OverridesStructIntercept(interceptorType, allocationFreeBase, invocationStruct, taskGeneric, isGeneric: true))
        {
            return ValueTaskLayerKind.AllocationFreeTaskBridge;
        }

        return ValueTaskLayerKind.ClassBridge;
    }

    public static TaskLayerKind ResolveTaskLayerKind(
        INamedTypeSymbol interceptorType,
        INamedTypeSymbol? allocationFreeBase,
        Compilation compilation)
    {
        if (allocationFreeBase == null || !InheritsFrom(interceptorType, allocationFreeBase))
        {
            return TaskLayerKind.ClassBridge;
        }

        var invocationStruct = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.AbpInvocationStructOpen);
        var task = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.Task);
        var taskGeneric = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.TaskOpen);

        if (invocationStruct == null || task == null || taskGeneric == null)
        {
            return TaskLayerKind.ClassBridge;
        }

        if (OverridesStructIntercept(interceptorType, allocationFreeBase, invocationStruct, task, isGeneric: false)
            || OverridesStructIntercept(interceptorType, allocationFreeBase, invocationStruct, taskGeneric, isGeneric: true))
        {
            return TaskLayerKind.AllocationFreeTask;
        }

        return TaskLayerKind.ClassBridge;
    }

    private static bool OverridesStructSyncIntercept(
        INamedTypeSymbol interceptorType,
        INamedTypeSymbol allocationFreeBase,
        INamedTypeSymbol invocationStruct)
    {
        return OverridesStructSyncInterceptMethod(interceptorType, allocationFreeBase, invocationStruct);
    }

    private static bool OverridesStructSyncInterceptMethod(
        INamedTypeSymbol interceptorType,
        INamedTypeSymbol allocationFreeBase,
        INamedTypeSymbol invocationStruct)
    {
        foreach (var member in interceptorType.GetMembers("InternalInterceptSynchronous"))
        {
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            if (method.TypeParameters.Length != 0 || method.Parameters.Length != 1)
            {
                continue;
            }

            if (method.Parameters[0].RefKind != RefKind.Ref)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, invocationStruct))
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(method.ContainingType, allocationFreeBase))
            {
                continue;
            }

            if (IsProceedOnlySyncOverride(method))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool OverridesStructIntercept(
        INamedTypeSymbol interceptorType,
        INamedTypeSymbol allocationFreeBase,
        INamedTypeSymbol invocationStructOpen,
        INamedTypeSymbol asyncType,
        bool isGeneric)
    {
        return OverridesStructInterceptMethod(interceptorType, allocationFreeBase, invocationStructOpen, asyncType, isGeneric);
    }

    private static bool OverridesStructInterceptMethod(
        INamedTypeSymbol interceptorType,
        INamedTypeSymbol allocationFreeBase,
        INamedTypeSymbol invocationStructOpen,
        INamedTypeSymbol asyncType,
        bool isGeneric)
    {
        foreach (var member in interceptorType.GetMembers("InternalInterceptAsynchronous"))
        {
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            if (isGeneric)
            {
                if (method.TypeParameters.Length != 1 || method.Parameters.Length != 1)
                {
                    continue;
                }
            }
            else if (method.TypeParameters.Length != 0 || method.Parameters.Length != 1)
            {
                continue;
            }

            if (method.Parameters[0].RefKind != RefKind.Ref)
            {
                continue;
            }

            if (method.Parameters[0].Type is not INamedTypeSymbol parameterType
                || !SymbolEqualityComparer.Default.Equals(parameterType.OriginalDefinition, invocationStructOpen))
            {
                continue;
            }

            var asyncArgument = parameterType.TypeArguments[0];
            if (isGeneric)
            {
                if (asyncArgument is not INamedTypeSymbol genericAsyncArgument
                    || !SymbolEqualityComparer.Default.Equals(genericAsyncArgument.OriginalDefinition, asyncType))
                {
                    continue;
                }
            }
            else if (!SymbolEqualityComparer.Default.Equals(asyncArgument, asyncType))
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(method.ContainingType, allocationFreeBase))
            {
                continue;
            }

            if (IsProceedOnlyStructOverride(method))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsProceedOnlySyncOverride(IMethodSymbol method)
    {
        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is not MethodDeclarationSyntax methodSyntax)
            {
                continue;
            }

            if (methodSyntax.ExpressionBody != null)
            {
                return IsProceedCall(methodSyntax.ExpressionBody.Expression);
            }

            if (methodSyntax.Body?.Statements.Count == 1
                && methodSyntax.Body.Statements[0] is ExpressionStatementSyntax expressionStatement)
            {
                return IsProceedCall(expressionStatement.Expression);
            }
        }

        return false;
    }

    private static bool IsProceedOnlyStructOverride(IMethodSymbol method)
    {
        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is not MethodDeclarationSyntax methodSyntax)
            {
                continue;
            }

            if (methodSyntax.ExpressionBody != null)
            {
                return IsProceedCall(methodSyntax.ExpressionBody.Expression);
            }

            if (methodSyntax.Body?.Statements.Count == 1
                && methodSyntax.Body.Statements[0] is ReturnStatementSyntax returnStatement)
            {
                return IsProceedCall(returnStatement.Expression);
            }
        }

        return false;
    }

    private static bool IsProceedCall(ExpressionSyntax? expression)
    {
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        return memberAccess.Name.Identifier.Text == "Proceed"
               && memberAccess.Expression.ToString() == "invocation";
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }
}
