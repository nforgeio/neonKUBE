using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace HelloWorld_DecisionTask
{
    [ActivityInterface(TaskQueue = "my-tasks")]
    public interface IMyActivity : IActivity
    {
        [ActivityMethod(Name = "get-email")]
        Task<string> GetEmailAsync(long customerId);

        [ActivityMethod(Name = "is-blocked")]
        Task<bool> IsBlockedAsync(long customerId);

        [ActivityMethod(Name = "send-email")]
        Task SendEmail(string email, string text);
    }

    [WorkflowInterface(TaskQueue = "my-tasks")]
    public interface IMyWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task DoItAsync(long customerId);
    }

    [Workflow(AutoRegister = true)]
    public class MyWorkflow : WorkflowBase, IMyWorkflow
    {
        public async Task DoItAsync(long customerId)
        {
            var stub = Workflow.NewActivityStub<IMyActivity>();     // <-- Decision task #1 starts here

            if (customerId <= 0)
            {
                return; // Invalid customer ID
            }

            await Workflow.SleepAsync(TimeSpan.FromSeconds(10));    // <-- Decision task #1 ends here

            if (await stub.IsBlockedAsync(customerId))
            {
                return;
            }

            var email = await stub.GetEmailAsync(customerId);

            await stub.SendEmail(email, "This is a test.");
        }
    }
}
