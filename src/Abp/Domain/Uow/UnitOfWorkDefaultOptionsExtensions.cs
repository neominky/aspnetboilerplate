using System;
using System.Linq;
using System.Reflection;
using Abp.Dependency;

namespace Abp.Domain.Uow
{
    internal static class UnitOfWorkDefaultOptionsExtensions
    {
        public static UnitOfWorkAttribute GetUnitOfWorkAttributeOrNull(this IUnitOfWorkDefaultOptions unitOfWorkDefaultOptions, MethodInfo methodInfo)
        {
            if (TryGetUnitOfWorkAttributeFromBakedMetadata(methodInfo, out var bakedAttribute))
            {
                return bakedAttribute;
            }

            var attrs = methodInfo.GetCustomAttributes(true).OfType<UnitOfWorkAttribute>().ToArray();
            if (attrs.Length > 0)
            {
                return attrs[0];
            }

            attrs = methodInfo.DeclaringType.GetTypeInfo().GetCustomAttributes(true).OfType<UnitOfWorkAttribute>().ToArray();
            if (attrs.Length > 0)
            {
                return attrs[0];
            }

            if (unitOfWorkDefaultOptions.IsConventionalUowClass(methodInfo.DeclaringType))
            {
                return new UnitOfWorkAttribute(); //Default
            }

            return null;
        }

        public static bool IsConventionalUowClass(this IUnitOfWorkDefaultOptions unitOfWorkDefaultOptions, Type type)
        {
            return unitOfWorkDefaultOptions.ConventionalUowSelectors.Any(selector => selector(type));
        }

        private static bool TryGetUnitOfWorkAttributeFromBakedMetadata(MethodInfo methodInfo, out UnitOfWorkAttribute attribute)
        {
            attribute = null;

            if (!AbpMethodInfo.TryGetMetadata(methodInfo, out var metadata) || metadata == null)
            {
                return false;
            }

            if (metadata.UnitOfWorkAttribute != null)
            {
                attribute = metadata.UnitOfWorkAttribute;
                return true;
            }

            if (metadata.ApplyConventionalUnitOfWork)
            {
                attribute = new UnitOfWorkAttribute();
                return true;
            }

            return true;
        }
    }
}
