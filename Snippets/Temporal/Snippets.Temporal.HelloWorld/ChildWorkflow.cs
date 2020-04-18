using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace HelloWorld_ChildWorkflow
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
            var smtp = new SmtpClient("mail.my-company.com");
            var message = new MailMessage("bot@my-company.com", email);

            message.Body = messageText;

            smtp.Send(message);
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

    [WorkflowInterface(TaskList = "my-tasks")]
    public interface IParentWorkfow : IWorkflow
    {
        [WorkflowMethod]
        Task DoEmailingAsync(string adminEmail);
    }

    [Workflow(AutoRegister = true)]
    public class ParentWorkflow : WorkflowBase, IParentWorkfow
    {
        public async Task DoEmailingAsync(string adminEmail)
        {
            var childStub    = Workflow.NewChildWorkflowStub<IEmailWorkflow>();
            var activityStub = Workflow.NewActivityStub<IEmailActivity>();

            await childStub.SendMessagesAsync();
            await activityStub.SendMessageAsync(adminEmail, "All emails were sent.");
        }
    }
    #endregion
}
