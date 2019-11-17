using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;

namespace HelloWorld_BasicActivity_2
{
    #region code
    [ActivityInterface(TaskList = "my-tasks")]
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
        }
    }

    [WorkflowInterface(TaskList = "my-tasks")]
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
            // Connect to Cadence

            var settings = new CadenceSettings()
            {
                DefaultDomain = "my-domain",
                CreateDomain  = true,
                Servers       = new List<string>() { "cadence://localhost:7933" }
            };

            using (var client = await CadenceClient.ConnectAsync(settings))
            {
                // Register your workflow and activity implementations to let 
                // Cadence know we're open for business.

                await client.RegisterWorkflowAsync<HelloWorkflow>();
                await client.RegisterActivityAsync<SendHelloActivity>();
                await client.StartWorkerAsync("my-tasks");

                // Invoke the workflow.

                var workflowStub = client.NewWorkflowStub<IHelloWorkflow>();

                await workflowStub.HelloAsync("jeff@my-company.com", "Jeff");
            }
        }
    }
    #endregion
}