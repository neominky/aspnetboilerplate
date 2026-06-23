using System;
using System.Collections.Generic;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    public static class CompileTimeIocRegistrarRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Type, Action<IIocManager>> Registrars = new Dictionary<Type, Action<IIocManager>>();

        public static void Register(Type moduleType, Action<IIocManager> register)
        {
            lock (SyncRoot)
            {
                Registrars[moduleType] = register;
            }
        }

        public static bool TryInvoke(Type moduleType, IIocManager iocManager)
        {
            Action<IIocManager>? register;
            lock (SyncRoot)
            {
                if (!Registrars.TryGetValue(moduleType, out register))
                {
                    return false;
                }
            }

            register?.Invoke(iocManager);
            return true;
        }
    }
}
