using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Application.Features;
using Abp.Configuration.Startup;
using Abp.Dependency;
using Abp.Localization;
using Abp.Reflection;
using Abp.Runtime.Session;

namespace Abp.Authorization
{
    /// <summary>
    /// Compile-time <see cref="IAuthorizationHelper"/> using <see cref="AbpMethodInfo"/> metadata.
    /// Moved from <c>src/Abp/Authorization/AuthorizationHelper.CompileTime.cs</c>.
    /// </summary>
    public class CompileTimeAuthorizationHelper : IAuthorizationHelper
    {
        public IAbpSession AbpSession { get; set; }
        public IPermissionChecker PermissionChecker { get; set; }
        public ILocalizationManager LocalizationManager { get; set; }

        private readonly IFeatureChecker _featureChecker;
        private readonly IAuthorizationConfiguration _authConfiguration;

        public CompileTimeAuthorizationHelper(IFeatureChecker featureChecker, IAuthorizationConfiguration authConfiguration)
        {
            _featureChecker = featureChecker;
            _authConfiguration = authConfiguration;
            AbpSession = NullAbpSession.Instance;
            PermissionChecker = NullPermissionChecker.Instance;
            LocalizationManager = NullLocalizationManager.Instance;
        }

        public virtual void Authorize(AbpMethodInfo method, Type type)
        {
            if (TryAuthorizeFromBuiltInMetadata(method, async: false))
            {
                return;
            }

            if (method.ReflectionMethod != null)
            {
                Authorize(method.ReflectionMethod, type);
            }
        }

        public virtual Task AuthorizeAsync(AbpMethodInfo method, Type type)
        {
            if (TryAuthorizeFromBuiltInMetadata(method, async: true))
            {
                return Task.CompletedTask;
            }

            if (method.ReflectionMethod != null)
            {
                return AuthorizeAsync(method.ReflectionMethod, type);
            }

            return Task.CompletedTask;
        }

        public virtual async Task AuthorizeAsync(IEnumerable<IAbpAuthorizeAttribute> authorizeAttributes)
        {
            if (!_authConfiguration.IsEnabled)
            {
                return;
            }

            if (!AbpSession.UserId.HasValue)
            {
                throw new AbpAuthorizationException(
                    LocalizationManager.GetString(AbpConsts.LocalizationSourceName, "CurrentUserDidNotLoginToTheApplication")
                    );
            }

            foreach (var authorizeAttribute in authorizeAttributes)
            {
                await PermissionChecker.AuthorizeAsync(authorizeAttribute.RequireAllPermissions, authorizeAttribute.Permissions);
            }
        }

        public virtual void Authorize(IEnumerable<IAbpAuthorizeAttribute> authorizeAttributes)
        {
            if (!_authConfiguration.IsEnabled)
            {
                return;
            }

            if (!AbpSession.UserId.HasValue)
            {
                throw new AbpAuthorizationException(
                    LocalizationManager.GetString(AbpConsts.LocalizationSourceName, "CurrentUserDidNotLoginToTheApplication")
                    );
            }

            foreach (var authorizeAttribute in authorizeAttributes)
            {
                PermissionChecker.Authorize(authorizeAttribute.RequireAllPermissions, authorizeAttribute.Permissions);
            }
        }

        public virtual async Task AuthorizeAsync(MethodInfo methodInfo, Type type)
        {
            await CheckFeaturesAsync(methodInfo, type);
            await CheckPermissionsAsync(methodInfo, type);
        }

        public virtual void Authorize(MethodInfo methodInfo, Type type)
        {
            CheckFeatures(methodInfo, type);
            CheckPermissions(methodInfo, type);
        }

        protected virtual async Task CheckFeaturesAsync(MethodInfo methodInfo, Type type)
        {
            var featureAttributes = ReflectionHelper.GetAttributesOfMemberAndType<RequiresFeatureAttribute>(methodInfo, type);

            if (featureAttributes.Count <= 0)
            {
                return;
            }

            foreach (var featureAttribute in featureAttributes)
            {
                await _featureChecker.CheckEnabledAsync(featureAttribute.RequiresAll, featureAttribute.Features);
            }
        }

        protected virtual void CheckFeatures(MethodInfo methodInfo, Type type)
        {
            var featureAttributes = ReflectionHelper.GetAttributesOfMemberAndType<RequiresFeatureAttribute>(methodInfo, type);

            if (featureAttributes.Count <= 0)
            {
                return;
            }

            foreach (var featureAttribute in featureAttributes)
            {
                _featureChecker.CheckEnabled(featureAttribute.RequiresAll, featureAttribute.Features);
            }
        }

        protected virtual async Task CheckPermissionsAsync(MethodInfo methodInfo, Type type)
        {
            if (!_authConfiguration.IsEnabled)
            {
                return;
            }

            if (AllowAnonymous(methodInfo, type))
            {
                return;
            }

            if (ReflectionHelper.IsPropertyGetterSetterMethod(methodInfo, type))
            {
                return;
            }

            if (!methodInfo.IsPublic && !methodInfo.GetCustomAttributes().OfType<IAbpAuthorizeAttribute>().Any())
            {
                return;
            }

            var authorizeAttributes =
                ReflectionHelper
                    .GetAttributesOfMemberAndType(methodInfo, type)
                    .OfType<IAbpAuthorizeAttribute>()
                    .ToArray();

            if (!authorizeAttributes.Any())
            {
                return;
            }

            await AuthorizeAsync(authorizeAttributes);
        }

        protected virtual void CheckPermissions(MethodInfo methodInfo, Type type)
        {
            if (!_authConfiguration.IsEnabled)
            {
                return;
            }

            if (AllowAnonymous(methodInfo, type))
            {
                return;
            }

            if (ReflectionHelper.IsPropertyGetterSetterMethod(methodInfo, type))
            {
                return;
            }

            if (!methodInfo.IsPublic && !methodInfo.GetCustomAttributes().OfType<IAbpAuthorizeAttribute>().Any())
            {
                return;
            }

            var authorizeAttributes =
                ReflectionHelper
                    .GetAttributesOfMemberAndType(methodInfo, type)
                    .OfType<IAbpAuthorizeAttribute>()
                    .ToArray();

            if (!authorizeAttributes.Any())
            {
                return;
            }

            Authorize(authorizeAttributes);
        }

        private void CheckFeaturesInternal(IReadOnlyList<RequiresFeatureAttribute> featureAttributes)
        {
            if (featureAttributes == null || featureAttributes.Count <= 0)
            {
                return;
            }

            foreach (var featureAttribute in featureAttributes)
            {
                _featureChecker.CheckEnabled(featureAttribute.RequiresAll, featureAttribute.Features);
            }
        }

        private Task CheckFeaturesInternalAsync(IReadOnlyList<RequiresFeatureAttribute> featureAttributes)
        {
            if (featureAttributes == null || featureAttributes.Count <= 0)
            {
                return Task.CompletedTask;
            }

            return CheckFeaturesInternalAsyncCore(featureAttributes);
        }

        private async Task CheckFeaturesInternalAsyncCore(IReadOnlyList<RequiresFeatureAttribute> featureAttributes)
        {
            foreach (var featureAttribute in featureAttributes)
            {
                await _featureChecker.CheckEnabledAsync(featureAttribute.RequiresAll, featureAttribute.Features);
            }
        }

        private bool TryAuthorizeFromBuiltInMetadata(AbpMethodInfo method, bool async)
        {
            if (method is not IAbpBuiltInInterceptionMetadata builtIn)
            {
                return false;
            }

            if (builtIn.AuthorizeAttributes == null && builtIn.FeatureAttributes == null)
            {
                return false;
            }

            if (builtIn.AllowAnonymous)
            {
                return true;
            }

            if (builtIn.FeatureAttributes is { Count: > 0 })
            {
                if (async)
                {
                    CheckFeaturesInternalAsync(builtIn.FeatureAttributes).GetAwaiter().GetResult();
                }
                else
                {
                    CheckFeaturesInternal(builtIn.FeatureAttributes);
                }
            }

            if (builtIn.AuthorizeAttributes is { Count: > 0 })
            {
                if (async)
                {
                    AuthorizeAsync(builtIn.AuthorizeAttributes).GetAwaiter().GetResult();
                }
                else
                {
                    Authorize(builtIn.AuthorizeAttributes);
                }
            }

            return true;
        }

        private static bool AllowAnonymous(MemberInfo memberInfo, Type type)
        {
            return ReflectionHelper
                .GetAttributesOfMemberAndType(memberInfo, type)
                .OfType<IAbpAllowAnonymousAttribute>()
                .Any();
        }
    }
}
