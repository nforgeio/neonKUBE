using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace Snippets.Temporal.CRON
{
    #region code
    [WorkflowInterface(TaskQueue = "my-tasks")]
    public interface ICronWorkflow : IWorkflow
    {
        [WorkflowMethod(Name = "backup")]
        Task BackupAsync();
    }

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // Connect to Temporal

            var settings = new TemporalSettings()
            {
                Namespace       = "my-namespace",
                CreateNamespace = true,
                HostPort        = "localhost:7933"
            };

            using (var client = await TemporalClient.ConnectAsync(settings))
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
