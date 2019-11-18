using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;

namespace Snippets.Cadence.CRON
{
    #region code
    [WorkflowInterface(TaskList = "my-tasks")]
    public interface ICronWorkflow : IWorkflow
    {
        [WorkflowMethod(Name = "backup")]
        Task BackupAsync();
    }

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // Connect to Cadence

            var settings = new CadenceSettings()
            {
                DefaultDomain = "my-domain",
                CreateDomain  = true,
                Servers       = new List<string>() { "cadence://localhost:7933" }
            };

            using (var client = await CadenceClient.ConnectAsync(settings))
            {
                var stub = client.NewWorkflowFutureStub<ICronWorkflow>(
                    "backup",
                    new WorkflowOptions()
                    {
                        // Run the workflow every day at 1:00am UTC:
                        CronSchedule = "0 1 * * *"
                    }); ;

                await stub.StartAsync();
            }
        }
    }
    #endregion
}
