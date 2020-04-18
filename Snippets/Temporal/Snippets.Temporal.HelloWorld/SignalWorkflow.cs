using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace Snippets_SignalWorkflow
{
    #region code
    [WorkflowInterface(TaskList = "my-tasks")]
    public interface IMyWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task DoItAsync();

        [SignalMethod("signal")]
        Task SignalAsync(string message);

        [QueryMethod("get-status")]
        Task<string> GetStatusAsync();
    }

    [Workflow]
    public class MyWorkflow : WorkflowBase, IMyWorkflow
    {
        private string                  state = "started";
        private WorkflowQueue<string>   signalQueue;

        public async Task DoItAsync()
        {
            signalQueue = await Workflow.NewQueueAsync<string>();

            while (true)
            {
                state = await signalQueue.DequeueAsync();

                if (state == "done")
                {
                    break;
                }
            }
        }

        public async Task SignalAsync(string message)
        {
            await signalQueue.EnqueueAsync(message);
        }

        public async Task<string> GetStatusAsync()
        {
            return await Task.FromResult(state);
        }
    }

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var settings = new TemporalSettings()
            {
                DefaultDomain = "my-domain",
                CreateDomain  = true,
                Servers       = new List<string>() { "temporal://localhost:7933" }
            };

            using (var client = await TemporalClient.ConnectAsync(settings))
            {
                await client.RegisterAssemblyAsync(System.Reflection.Assembly.GetExecutingAssembly());
                await client.StartWorkerAsync("my-tasks");

                // Invoke the workflow, send it some signals and very that
                // it changed its state to the signal value.

                var stub = client.NewWorkflowStub<IMyWorkflow>();
                var task = stub.DoItAsync();

                await stub.SignalAsync("signal #1");
                Console.WriteLine(await stub.GetStatusAsync());

                await stub.SignalAsync("signal #2");
                Console.WriteLine(await stub.GetStatusAsync());

                // This signal completes the workflow.

                await stub.SignalAsync("done");
                await task;
            }
        }
    }
    #endregion
}
