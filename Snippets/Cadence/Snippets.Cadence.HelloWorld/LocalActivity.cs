using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;

namespace HelloWorld_LocalActivity
{
    #region code
    [ActivityInterface]
    public interface IMyActivity : IActivity
    {
        [ActivityMethod]
        Task<string> HelloAsync(string name);
    }

    [Activity]
    public class MyActivity : ActivityBase, IMyActivity
    {
        public async Task<string> HelloAsync(string name)
        {
            return await Task.FromResult($"Hello {name}!");
        }
    }

    [WorkflowInterface(TaskList = "my-tasks")]
    public interface IMyWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task<string> RunAsync();
    }

    [Workflow(AutoRegister = true)]
    public class MyWorkflow : WorkflowBase, IMyWorkflow
    {
        public async Task<string> RunAsync()
        {
            var stub = Workflow.NewLocalActivityStub<IMyActivity, MyActivity>();

            return await stub.HelloAsync("Jeff");
        }
    }
    #endregion
}
