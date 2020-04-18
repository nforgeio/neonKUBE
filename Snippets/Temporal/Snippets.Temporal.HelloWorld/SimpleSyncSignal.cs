using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

#pragma warning disable CS0414 // Field assigned but not used

namespace Snippets_SimpleSignalWorkflow
{
    #region code
    [WorkflowInterface(TaskList = "my-tasks")]
    public interface IOrderWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task<bool> ProcessOrderAsync();

        [SignalMethod("cancel", Synchronous = true)]
        Task<string> CancelOrderAsync(string reason);
    }

    [Workflow]
    public class OrderWorkflow : WorkflowBase, IOrderWorkflow
    {
        private bool    cancelPending = false;
        private string  cancelReason  = null;
        private bool    canCancel     = true;

        public async Task<bool> ProcessOrderAsync()
        {
            // Implements order processing.  This is probably includes
            // one or more loops that poll [canCancel] while it's still
            // possible to cancel the order.

            await Workflow.SleepAsync(TimeSpan.FromSeconds(5));

            if (cancelPending)
            {
                return false;
            }

            // Cancellation is no longer alloowed.

            canCancel = false;

            // This is where the order will be fulfilled.

            await Workflow.SleepAsync(TimeSpan.FromSeconds(5));

            return true;
        }

        public async Task<string> CancelOrderAsync(string reason)
        {
            if (!canCancel)
            {
                return "Order can no longer be be cancelled";
            }

            cancelPending = true;
            cancelReason  = reason;

            return "Order cancelled";
        }
    }
    #endregion
}
