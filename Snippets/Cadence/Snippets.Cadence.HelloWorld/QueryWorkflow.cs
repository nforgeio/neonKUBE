using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;

namespace Snippets_QueryWorkflow
{
    #region code
    [WorkflowInterface(TaskList = "my-tasks")]
    public interface IMyWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task DoItAsync();

        [QueryMethod("get-status")]
        Task<string> GetStatusAsync();
    }

    [Workflow]
    public class MyWorkflow : WorkflowBase, IMyWorkflow
    {
        private string state = "started";

        public async Task DoItAsync()
        {
            var sleepTime = TimeSpan.FromSeconds(5);

            state = "sleeping #1";
            await Workflow.SleepAsync(sleepTime);

            state = "sleeping #2";
            await Workflow.SleepAsync(sleepTime);

            state = "sleeping #3";
            await Workflow.SleepAsync(sleepTime);

            state = "done";
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
            var settings = new CadenceSettings()
            {
                DefaultDomain = "my-domain",
                CreateDomain  = true,
                Servers       = new List<string>() { "cadence://localhost:7933" }
            };

            using (var client = await CadenceClient.ConnectAsync(settings))
            {
                await client.RegisterAssemblyAsync(System.Reflection.Assembly.GetExecutingAssembly());
                await client.StartWorkerAsync("my-tasks");

                // Invoke the workflow and then query it's status a few times.

                var stub = client.NewWorkflowStub<IMyWorkflow>();
                var task = stub.DoItAsync();

                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2.5));

                    Console.WriteLine(await stub.GetStatusAsync());
                }

                await task;
            }
        }
    }
    #endregion
}
