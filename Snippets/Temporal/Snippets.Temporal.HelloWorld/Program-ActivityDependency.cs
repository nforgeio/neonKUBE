namespace HelloWorld_ActivityDependency
{
    #region code
    using System;
    using System.Collections.Generic;
    using System.Net.Mail;
    using System.Reflection;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;

    using Neon.Common;
    using Neon.Temporal;

    // An instance of this class will be injected into the activity.

    public class MailSettings
    {
        public string MailServer { get; set; }
    }

    [ActivityInterface(TaskList = "my-tasks")]
    public interface IEmailActivity : IActivity
    {
        [ActivityMethod(Name = "send-message")]
        Task SendMessageAsync(string email, string messageText);
    }

    [Activity(AutoRegister = true)]
    public class EmailActivity : ActivityBase, IEmailActivity
    {
        private MailSettings settings;

        public EmailActivity(MailSettings settings)
        {
            // The [settings] parameter was injected into this activity
            // instance.  We'll save it to a local field so it will
            // be available to all of the activity methods.

            this.settings = settings;
        }

        public async Task SendMessageAsync(string email, string messageText)
        {
            var smtp    = new SmtpClient(settings.MailServer);   // Connect using the injected settings
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

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure the settings name such that they will be injected
            // into the email activity when it's constructed.
            //
            // Note that we did this before calling RegisterAssemblyAsync() below.
            // Dependencies added after activities have been registered will be
            // ignored.

            NeonHelper.ServiceContainer.AddSingleton(typeof(MailSettings), new MailSettings() { MailServer = "mail.my-company.com" });

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