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
            // Create queue to receive signal requests from the 
            // [CancelOrderAsync()] synchronous signal method.

            queue = await Workflow.NewQueueAsync<SignalRequest<string>>();

            // Wait for a signal request to be dequeued, and obtain the 
            // "reason" argument.  This will be the "reason" parameter 
            // value passed to [CancelOrderAsync()] below.

            var signal = await queue.DequeueAsync();
            var reason = signal.Arg<string>("reason");

            // This line actually specifies the result to be be returned
            // to the external code that sent the synchronous signal.

            await signal.ReplyAsync($"Order cancelled due to: {reason}");
        }

        public async Task<string> CancelOrderAsync(string reason)
        {
            // This line creates a [SignalRequest] instance that somewhat
            // magically initializes the [SignalRequest.Args] dictionary
            // with the names and values of the parameters passed to this
            // method.

            var signalRequest = new SignalRequest<string>();

            // Enqueue the signal request such that workflow method above
            // can process it as part of the workflow logic.

            await queue.EnqueueAsync(signalRequest);

            // Throwing this exception indicates to the Cadence client
            // that the signal result will be sent as a reply from
            // the workflow code via the [SignalRequest] rather than 
            // via a result returned by this signal method.

            throw new WaitForSignalReplyException();
        }
    }
    #endregion
}
