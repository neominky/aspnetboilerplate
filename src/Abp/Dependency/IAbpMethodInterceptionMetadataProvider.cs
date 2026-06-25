using System.Reflection;

namespace Abp.Dependency
{
    public interface IAbpMethodInterceptionMetadataProvider
    {
        void Register(MethodInfo method, AbpMethodInterceptionMetadata metadata);

        bool TryGet(MethodInfo method, out AbpMethodInterceptionMetadata? metadata);
    }
}
