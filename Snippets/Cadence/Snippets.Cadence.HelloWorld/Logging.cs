namespace Logging
{
    #region code
    using System;
    using System.Collections.Generic;
    using System.Net.Mail;
    using System.Threading.Tasks;

    using Neon.Cadence;
    using Neon.Common;
    using Neon.Diagnostics;

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
            try
            {
                Activity.Logger.LogDebug("GetEmailListAsync: started");

                // Pretend that this activity is querying a database or REST API to
                // obtain the email list.

                var list = new List<string>();

                list.Add("jeff@my-company.com");
                list.Add("jill@my-company.com");
                list.Add("jack@my-company.com");
                list.Add("nancy@my-company.com");

                return await Task.FromResult(list);
            }
            catch (Exception e)
            {
                Activity.Logger.LogError(e);
                throw;
            }
            finally
            {
                Activity.Logger.LogDebug("GetEmailListAsync: finished");
            }
        }

        public async Task SendMessageAsync(string email, string messageText)
        {
            try
            {
                Activity.Logger.LogDebug("SendMessageAsync: started");

                var smtp = new SmtpClient("mail.my-company.com");
                var message = new MailMessage("bot@my-company.com", email);

                message.Body = messageText;

                smtp.Send(message);

                await Task.CompletedTask;
            }
            catch (Exception e)
            {
                Activity.Logger.LogError(e);
                throw;
            }
            finally
            {
                Activity.Logger.LogDebug("SendMessageAsync: finished");
            }
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
            try
            {
                Workflow.Logger.LogInfo("SendMessagesAsync: started");

                var activityStub = Workflow.NewActivityStub<IEmailActivity>();
                var emailList    = await activityStub.GetEmailListAsync();

                foreach (var email in emailList)
                {
                    await activityStub.SendMessageAsync(email, "This is a test message.");
                }
            }
            catch (Exception e)
            {
                Workflow.Logger.LogError(e);
                throw;
            }
            finally
            {
                Workflow.Logger.LogInfo("SendMessagesAsync: finished");
            }
        }
    }

    public static class Program
    {
        private static INeonLogger logger;

        public static async Task Main(string[] args)
        {
            // Initialize the logger.

            LogManager.Default.SetLogLevel("info");

            logger = LogManager.Default.GetLogger(typeof(Program));
            logger.LogInfo("Starting workflow service");

            try
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

                    await client.RegisterAssemblyAsync(System.Reflection.Assembly.GetExecutingAssembly());
                    await client.StartWorkerAsync("my-tasks");

                    // Spin forever, processing workflows and activities assigned by Cadence.

                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5));
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e);
            }
            finally
            {
                logger.LogInfo("Exiting workflow service");
            }
        }
    }
    #endregion
}
