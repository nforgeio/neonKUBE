using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace HelloWorld_BasicActivity_1
{
    #region code
    [ActivityInterface(TaskList = "my-tasks")]
    public interface ISendHelloActivity : IActivity
    {
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
    #endregion
}