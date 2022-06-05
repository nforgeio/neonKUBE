#pragma warning disable CS8892 // Method 'Program.Main(string[])' will not be used as an entry point because a synchronous entry point 'Program.Main(string[])' was found.

namespace HelloWorld_ActivityDependency
{
    #region code
    using System;
    using System.Collections.Generic;
    using System.Net.Mail;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;

    using Neon.Cadence;
    using Neon.Common;

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

                await client.RegisterAssemblyAsync(System.Reflection.Assembly.GetExecutingAssembly());
                await client.StartWorkerAsync("my-tasks");

                // Invoke the workflow.

                var workflowStub = client.NewWorkflowStub<IEmailWorkflow>();

                await workflowStub.SendMessagesAsync();
            }
        }
    }
    #endregion
}