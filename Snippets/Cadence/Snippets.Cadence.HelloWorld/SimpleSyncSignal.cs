using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;

namespace Snippets_SimpleSignalWorkflow
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
        private bool    cancelPending  = false;
        private string  cancelReason   = null;
        private bool    canCancel      = true;

        public async Task ProcessOrderAsync()
        {
            // Implements order processing.  This is probably includes
            // one or more loops that poll [canCancel] while it's still
            // possible to cancel the order.
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
