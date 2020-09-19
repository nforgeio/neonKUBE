using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace HelloWorld_Parallel
{
    #region code
    [ActivityInterface(TaskQueue = "my-tasks")]
    public interface IMyActivity : IActivity
    {
        [ActivityMethod(Name = "run-activity")]
        Task RunActivityAsync();
    }

    [WorkflowInterface(TaskQueue = "my-tasks")]
    public interface IChildWorkflow : IWorkflow
    {
        [ActivityMethod(Name = "run")]
        Task<List<string>> RunAsync(string name);
    }

    [WorkflowInterface(TaskQueue = "my-tasks")]
    public interface IParallelWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task<string> RunAsync();
    }

    [Workflow(AutoRegister = true)]
    public class ParallelWorkflow : WorkflowBase, IParallelWorkflow
    {
        public async Task<string> RunAsync()
        {
            var childStub    = Workflow.NewChildWorkflowFutureStub<IChildWorkflow>("run");
            var activityStub = Workflow.NewActivityFutureStub<IMyActivity>("run-activity");

            var childFuture    = await childStub.StartAsync<string>("Jeff");
            var activityFuture = await activityStub.StartAsync();

            await activityFuture.GetAsync();

            return await childFuture.GetAsync();
        }
    }
    #endregion
}
