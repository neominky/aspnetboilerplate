using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Abp.Application.Features;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.Timing;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Executes cross-cutting concerns for compile-time generated interceptors without reflection.
    /// </summary>
    public sealed class CompileTimeAspectExecutor : ITransientDependency
    {
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IAuthorizationHelper _authorizationHelper;
        private readonly IFeatureChecker _featureChecker;
        private readonly IAuditingHelper _auditingHelper;
        private readonly IAuditingConfiguration _auditingConfiguration;
        private readonly IAuditSerializer _auditSerializer;
        private readonly IAbpSession _abpSession;
        private readonly IAuditInfoProvider _auditInfoProvider;

        public CompileTimeAspectExecutor(
            IUnitOfWorkManager unitOfWorkManager,
            IAuthorizationHelper authorizationHelper,
            IFeatureChecker featureChecker,
            IAuditingHelper auditingHelper,
            IAuditingConfiguration auditingConfiguration,
            IAuditSerializer auditSerializer,
            IAbpSession abpSession,
            IAuditInfoProvider auditInfoProvider)
        {
            _unitOfWorkManager = unitOfWorkManager;
            _authorizationHelper = authorizationHelper;
            _featureChecker = featureChecker;
            _auditingHelper = auditingHelper;
            _auditingConfiguration = auditingConfiguration;
            _auditSerializer = auditSerializer;
            _abpSession = abpSession;
            _auditInfoProvider = auditInfoProvider;
        }

        public void Invoke(Action action, CompileTimeMethodAspectOptions options)
        {
            InvokeAsync(() =>
            {
                action();
                return Task.CompletedTask;
            }, options).GetAwaiter().GetResult();
        }

        public T Invoke<T>(Func<T> action, CompileTimeMethodAspectOptions options)
        {
            return InvokeAsync(() => Task.FromResult(action()), options).GetAwaiter().GetResult();
        }

        public Task InvokeAsync(Func<Task> action, CompileTimeMethodAspectOptions options)
        {
            return InvokeAsync(async () =>
            {
                await action();
                return 0;
            }, options);
        }

        public async Task<T> InvokeAsync<T>(Func<Task<T>> action, CompileTimeMethodAspectOptions options)
        {
            await AuthorizeAsync(options);

            if (options.Validate)
            {
                // Validation is emitted at call sites with explicit arguments.
            }

            if (options.UnitOfWork == null)
            {
                return await InvokeWithAuditingAsync(action, options);
            }

            using (var uow = _unitOfWorkManager.Begin(options.UnitOfWork))
            {
                var result = await InvokeWithAuditingAsync(action, options);
                await uow.CompleteAsync();
                return result;
            }
        }

        private async Task AuthorizeAsync(CompileTimeMethodAspectOptions options)
        {
            if (options.AllowAnonymous)
            {
                return;
            }

            if (options.FeatureAttributes != null)
            {
                foreach (var featureAttribute in options.FeatureAttributes)
                {
                    await _featureChecker.CheckEnabledAsync(featureAttribute.RequiresAll, featureAttribute.Features);
                }
            }

            if (options.AuthorizeAttributes != null && options.AuthorizeAttributes.Length > 0)
            {
                await _authorizationHelper.AuthorizeAsync(options.AuthorizeAttributes);
            }
        }

        private async Task<T> InvokeWithAuditingAsync<T>(Func<Task<T>> action, CompileTimeMethodAspectOptions options)
        {
            if (!options.Audit)
            {
                return await action();
            }

            var auditInfo = new AuditInfo
            {
                TenantId = _abpSession.TenantId,
                UserId = _abpSession.UserId,
                ImpersonatorUserId = _abpSession.ImpersonatorUserId,
                ImpersonatorTenantId = _abpSession.ImpersonatorTenantId,
                ServiceName = options.ServiceName,
                MethodName = options.MethodName,
                Parameters = options.AuditParameters == null
                    ? "{}"
                    : _auditSerializer.Serialize(options.AuditParameters),
                ExecutionTime = Clock.Now
            };

            try
            {
                _auditInfoProvider.Fill(auditInfo);
            }
            catch
            {
                // ignored - matches AuditingHelper behavior
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await action();

                if (_auditingConfiguration.SaveReturnValues && result != null)
                {
                    auditInfo.ReturnValue = _auditSerializer.Serialize(result);
                }

                return result;
            }
            catch (Exception ex)
            {
                auditInfo.Exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                auditInfo.ExecutionDuration = Convert.ToInt32(stopwatch.Elapsed.TotalMilliseconds);
                await _auditingHelper.SaveAsync(auditInfo);
            }
        }
    }
}
