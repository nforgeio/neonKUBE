using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace HelloWorld_BasicActivity_2
{
    #region code
    [ActivityInterface(TaskQueue = "my-tasks")]
    public interface ISendHelloActivity : IActivity
    {
        [ActivityMethod]
        Task SendHelloAsync(string email, string name);
    }

    public class SendHelloActivity : ActivityBase, ISendHelloActivity
    {
        public async Task SendHelloAsync(string email, string name)
        {
            var smtp    = new SmtpClient("mail.my-company.com");
            var message = new MailMessage("bot@my-company.com", email);

            message.Body = $"Hello {name}!";

            smtp.Send(message);
            await Task.CompletedTask;
        }
    }

    [WorkflowInterface(TaskQueue = "my-tasks")]
    public interface IHelloWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task HelloAsync(string email, string name);
    }

    public class HelloWorkflow : WorkflowBase, IHelloWorkflow
    {
        public async Task HelloAsync(string email, string name)
        {
            var activityStub = Workflow.NewActivityStub<ISendHelloActivity>();

            await activityStub.SendHelloAsync(email, name);
        }
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
                // Create a worker and register the workflow and activity 
                // implementations to let Temporal know we're open for business.

                var worker = await client.NewWorkerAsync();

                await worker.RegisterWorkflowAsync<HelloWorkflow>();
                await worker.RegisterActivityAsync<SendHelloActivity>();
                await worker.StartAsync();

                // Invoke the workflow.

                var workflowStub = client.NewWorkflowStub<IHelloWorkflow>();

                await workflowStub.HelloAsync("jeff@my-company.com", "Jeff");
            }
        }
    }
    #endregion
}