using System.Reflection;

namespace Abp.Dependency
{
    public static class AbpMethodInterceptionMetadataExtensions
    {
        public static bool TryGetInterceptionMetadata(
            this MethodInfo method,
            out AbpMethodInterceptionMetadata? metadata)
        {
            return AbpMethodInfo.TryGetMetadata(method, out metadata);
        }
    }
}
