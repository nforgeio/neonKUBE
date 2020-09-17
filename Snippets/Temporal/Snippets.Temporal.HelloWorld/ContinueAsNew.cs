using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Temporal;

namespace HelloWorld_ContinueAsNew_1
{
    #region code_loop
    public class CustomerInfo
    {
        public long         Id { get; set; }
        public string       Email { get; set; }
        public DateTime     SignupTimeUtc { get; set; }
        public bool         WelcomeSent { get; set; }
        public DateTime?    LastMarketingPingUtc { get; set; }
    }

    [ActivityInterface(TaskQueue = "my-tasks")]
    public interface ICustomerActivities : IActivity
    {
        [ActivityMethod(Name = "get-customer-info")]
        Task<CustomerInfo> GetCustomerInfo(long id);

        [ActivityMethod(Name = "save-customer")]
        Task UpdateCustomerInfo(CustomerInfo customer);

        [ActivityMethod(Name = "send-email")]
        Task SendEmail(string email, string message);
    }

    [Activity]
    public class CustomerActivities : ActivityBase, ICustomerActivities
    {
        public async Task<CustomerInfo> GetCustomerInfo(long id)
        {
            // Pretend that we're getting this from a database.

            return await Task.FromResult(
                new CustomerInfo()
                {
                    Id                   = id,
                    Email                = "jeff@my-company.com",
                    SignupTimeUtc        = new DateTime(2019, 11, 18, 11, 4,0 , DateTimeKind.Utc),
                    WelcomeSent          = false,
                    LastMarketingPingUtc = null
                });
        }

        public async Task UpdateCustomerInfo(CustomerInfo customer)
        {
            // Pretend that we're persisting the customer to a database.

            await Task.CompletedTask;
        }

        public async Task SendEmail(string email, string message)
        {
            // Pretend that we're sending an email here.

            await Task.CompletedTask;
        }
    }

    [WorkflowInterface(TaskQueue = "my-tasks")]
    public interface IEngagementWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task RunAsync(long customerId);
    }

    [Workflow(AutoRegister = true)]
    public class EngagementWorkflow : WorkflowBase, IEngagementWorkflow
    {
        public async Task RunAsync(long customerId)
        {
            var stub = Workflow.NewActivityStub<ICustomerActivities>();

            while (true)
            {
                var customer = await stub.GetCustomerInfo(customerId);
                var utcNow   = await Workflow.UtcNowAsync();

                if (!customer.WelcomeSent)
                {
                    await stub.SendEmail(customer.Email, "Welcome to our amazing service!");
                    customer.WelcomeSent = true;
                    await stub.UpdateCustomerInfo(customer);
                }
                else if (!customer.LastMarketingPingUtc.HasValue || 
                         customer.LastMarketingPingUtc.Value - utcNow >= TimeSpan.FromDays(7))
                {
                    await stub.SendEmail(customer.Email, "Weekly email bugging you to buy something!");
                    customer.LastMarketingPingUtc = utcNow;
                    await stub.UpdateCustomerInfo(customer);
                }

                await Workflow.SleepAsync(TimeSpan.FromMinutes(30));
            }
        }
    }
    #endregion
}

namespace HelloWorld_ContinueAsNew_2
{
    public class CustomerInfo
    {
        public long         Id { get; set; }
        public string       Email { get; set; }
        public DateTime     SignupTimeUtc { get; set; }
        public bool         WelcomeSent { get; set; }
        public DateTime?    LastMarketingPingUtc { get; set; }
    }

    [ActivityInterface(TaskQueue = "my-tasks")]
    public interface ICustomerActivities : IActivity
    {
        [ActivityMethod(Name = "get-customer-info")]
        Task<CustomerInfo> GetCustomerInfo(long id);

        [ActivityMethod(Name = "save-customer")]
        Task UpdateCustomerInfo(CustomerInfo customer);

        [ActivityMethod(Name = "send-email")]
        Task SendEmail(string email, string message);
    }

    [Activity]
    public class CustomerActivities : ActivityBase, ICustomerActivities
    {
        public async Task<CustomerInfo> GetCustomerInfo(long id)
        {
            // Pretend that we're getting this from a database.

            return await Task.FromResult(
                new CustomerInfo()
                {
                    Id                   = id,
                    Email                = "jeff@my-company.com",
                    SignupTimeUtc        = new DateTime(2019, 11, 18, 11, 4, 0, DateTimeKind.Utc),
                    WelcomeSent          = false,
                    LastMarketingPingUtc = null
                });
        }

        public async Task UpdateCustomerInfo(CustomerInfo customer)
        {
            // Pretend that we're persisting the customer to a database.

            await Task.CompletedTask;
        }

        public async Task SendEmail(string email, string message)
        {
            // Pretend that we're sending an email here.

            await Task.CompletedTask;
        }
    }

    [WorkflowInterface(TaskQueue = "my-tasks")]
    public interface IEngagementWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task RunAsync(long customerId);
    }

    #region code_continue
    [Workflow(AutoRegister = true)]
    public class EngagementWorkflow : WorkflowBase, IEngagementWorkflow
    {
        public async Task RunAsync(long customerId)
        {
            var stub     = Workflow.NewActivityStub<ICustomerActivities>();
            var customer = await stub.GetCustomerInfo(customerId);
            var utcNow   = await Workflow.UtcNowAsync();

            if (!customer.WelcomeSent)
            {
                await stub.SendEmail(customer.Email, "Welcome to our amazing service!");
                customer.WelcomeSent = true;
                await stub.UpdateCustomerInfo(customer);
            }
            else if (!customer.LastMarketingPingUtc.HasValue ||
                     customer.LastMarketingPingUtc.Value - utcNow >= TimeSpan.FromDays(7))
            {
                await stub.SendEmail(customer.Email, "Weekly email bugging you to buy something!");
                customer.LastMarketingPingUtc = utcNow;
                await stub.UpdateCustomerInfo(customer);
            }

            await Workflow.SleepAsync(TimeSpan.FromMinutes(30));
            await Workflow.ContinueAsNewAsync(customerId);
        }
    }
    #endregion
}
