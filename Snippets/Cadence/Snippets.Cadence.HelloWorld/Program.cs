using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cadence;

namespace HelloWorld
{
    [WorkflowInterface(TaskList = "my-tasks")]
    public interface IHelloWorkflow : IWorkflow
    {
        Task<string> HelloAsync(string name);
    }

    public class HelloWorkflow : WorkflowBase, IHelloWorkflow
    {
        public async Task<string> HelloAsync(string name)
        {
            return await Task.FromResult($"Hello {name}!");
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
                await client.RegisterWorkflowAsync<HelloWorkflow>();

                using (await client.StartWorkerAsync("my-tasks"))
                {
                    var stub = client.NewWorkflowStub<IHelloWorkflow>();

                    Console.WriteLine(await stub.HelloAsync("Jeff"));
                }
            }
        }
    }
}
