using System;
using System.Reflection;
using Abp.Dependency;

namespace Abp.Runtime.Validation.Interception
{
    public interface IMethodInvocationValidator
    {
        void Initialize(MethodInfo method, object[] parameterValues);

        void Validate();
    }
}
