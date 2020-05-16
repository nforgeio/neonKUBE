using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace HelloWorld_MultiStep
{
    #region code
    [ActivityInterface(TaskList = "my-tasks")]
    public interface IEmailActivity : IActivity
    {
        [ActivityMethod(Name = "get-email-list")]
        Task<List<string>> GetEmailListAsync();

        [ActivityMethod(Name = "send-message")]
        Task SendMessageAsync(string email, string messageText);
    }

    [Activity(AutoRegister = true)]
    public class EmailActivity : ActivityBase, IEmailActivity
    {
        public async Task<List<string>> GetEmailListAsync()
        {
            // Pretend that this activity is querying a database or REST API to
            // obtain the email list.

            var list = new List<string>();

            list.Add("jeff@my-company.com");
            list.Add("jill@my-company.com");
            list.Add("jack@my-company.com");
            list.Add("nancy@my-company.com");

            return await Task.FromResult(list);
        }

        public async Task SendMessageAsync(string email, string messageText)
        {
            var smtp    = new SmtpClient("mail.my-company.com");
            var message = new MailMessage("bot@my-company.com", email);

            message.Body = messageText;

            smtp.Send(message);
            await Task.CompletedTask;
        }
    }

    [WorkflowInterface(TaskList = "my-tasks")]
    public interface IEmailWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task SendMessagesAsync();
    }

    [Workflow(AutoRegister = true)]
    public class EmailWorkflow : WorkflowBase, IEmailWorkflow
    {
        public async Task SendMessagesAsync()
        {
            var activityStub = Workflow.NewActivityStub<IEmailActivity>();
            var emailList    = await activityStub.GetEmailListAsync();

            foreach (var email in emailList)
            {
                await activityStub.SendMessageAsync(email, "This is a test message.");
            }
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

                var worker = await client.NewWorkerAsync(new WorkerOptions() { TaskList = "my-tasks" });

                await worker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly());
                await worker.StartAsync();

                // Invoke the workflow.

                var workflowStub = client.NewWorkflowStub<IEmailWorkflow>();

                await workflowStub.SendMessagesAsync();
            }
        }
    }
    #endregion
}
