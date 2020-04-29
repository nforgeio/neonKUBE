using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace Snippets_CrossPlatform
{
    public static class Program
    {
        public static async Task UntypedStub()
        {
#region code_untyped
            var settings = new TemporalSettings()
            {
                // This specifies the default namespace for operations initiated by the
                // client connected below (this can be overridden for specific
                // operations).

                DefaultNamespace = "Acme-PROD",

                // Host/port for at least one of the Temporal cluster servers:

                HostPort = "localhost:7933"
            };

            using (var client = await TemporalClient.ConnectAsync(settings))
            {
                // Create an untyped stub for the new workflow.

                var stub = client.NewUntypedWorkflowStub("SendEmail");

                // Start the workflow.  Note that we need to take to ensure that the number, order
                // and types of the parameters match what the GOLANG workflow implementation expects.
                // Untyped workflow stub arguments cannot be checked be the C# compiler.
                //
                // This method returns a [WorkflowExecution] which includes the workflow and
                // run IDs.  We're not using these here, but real applications may want to
                // persist this so that could check on long-running workflows later.

                var execution = await stub.StartAsync("jeff@lilltek.com", "Test subject", "This is a test email.");

                // Wait for the workflow to complete and return it's result.  Note that we need
                // to explicitly specify the result [bool] type as a generic type parameter.
                // You need to ensure that this matches the workflow implementation as well.

                var success = await stub.GetResultAsync<bool>();

                if (success)
                {
                    Console.WriteLine("Email SENT!");
                }
                else
                {
                    Console.WriteLine("Email FAILED!");
                }
            }
#endregion
        }
    }
}
