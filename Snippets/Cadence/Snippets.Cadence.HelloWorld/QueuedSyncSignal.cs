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

            // Throwing this exception indicates to the Cadence client
            // that the signal result will be sent as a reply from
            // the workflow code via the [SignalRequest] enqueued
            // above rather than via a result returned by this
            // signal method.

            throw new WaitForSignalReplyException();
        }
    }
    #endregion
}
