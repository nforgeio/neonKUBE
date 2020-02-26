using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;

namespace Snippets_QueuedSignalWorkflow
{
    #region code
    [WorkflowInterface(TaskList = "my-tasks")]
    public interface IOrderWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task ProcessOrderAsync();

        [SignalMethod("cancel", Synchronous = true)]
        Task<string> CancelOrderAsync(string reason);
    }

    [Workflow]
    public class OrderWorkflow : WorkflowBase, IOrderWorkflow
    {
        private WorkflowQueue<SignalRequest<string>> queue;

        public async Task ProcessOrderAsync()
        {
            queue = await Workflow.NewQueueAsync<SignalRequest<string>>();

            var signal = await queue.DequeueAsync();
            var reason = signal.Arg<string>("reason");

            await signal.ReplyAsync("Order cancelled");
        }

        public async Task<string> CancelOrderAsync(string reason)
        {
            await queue.EnqueueAsync(new SignalRequest<string>());
            throw new WaitForSignalReplyException();
        }
    }
    #endregion
}
