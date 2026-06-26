using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.EntityHistory
{
    internal class EntityHistoryInterceptor : AbpInterceptorBase, ITransientDependency
    {
        private readonly IEntityHistoryUseCaseDescriptionProvider _useCaseDescriptionProvider;

        public IEntityChangeSetReasonProvider ReasonProvider { get; set; }

        public EntityHistoryInterceptor(IEntityHistoryUseCaseDescriptionProvider useCaseDescriptionProvider)
        {
            _useCaseDescriptionProvider = useCaseDescriptionProvider;
            ReasonProvider = NullEntityChangeSetReasonProvider.Instance;
        }

        public override void InterceptSynchronous(IAbpInvocation invocation)
        {
            var useCaseDescription = _useCaseDescriptionProvider.GetUseCaseDescription(invocation);

            if (useCaseDescription == null)
            {
                invocation.Proceed();
                return;
            }

            using (ReasonProvider.Use(useCaseDescription))
            {
                invocation.Proceed();
            }
        }

        protected override async Task InternalInterceptAsynchronous(IAbpInvocation invocation)
        {
            var proceedInfo = invocation.CaptureProceedInfo();

            var useCaseDescription = _useCaseDescriptionProvider.GetUseCaseDescription(invocation);

            if (useCaseDescription == null)
            {
                proceedInfo.Invoke();
                var task = (Task)invocation.ReturnValue;
                await task;
                return;
            }

            using (ReasonProvider.Use(useCaseDescription))
            {
                proceedInfo.Invoke();
                var task = (Task)invocation.ReturnValue;
                await task;
            }
        }

        protected override async Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
        {
            var proceedInfo = invocation.CaptureProceedInfo();

            var useCaseDescription = _useCaseDescriptionProvider.GetUseCaseDescription(invocation);

            if (useCaseDescription == null)
            {
                proceedInfo.Invoke();
                var taskResult = (Task<TResult>)invocation.ReturnValue;
                return await taskResult;
            }

            using (ReasonProvider.Use(useCaseDescription))
            {
                proceedInfo.Invoke();
                var taskResult = (Task<TResult>)invocation.ReturnValue;
                return await taskResult;
            }
        }
    }
}
