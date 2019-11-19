using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Xunit;

namespace MyTests
{
    [WorkflowInterface(TaskList = "test-tasks")]
    public interface IHelloWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task<string> HelloAsync(string name);
    }

    [Workflow(AutoRegister = true)]
    public class HelloWorkflow : WorkflowBase, IHelloWorkflow
    {
        public async Task<string> HelloAsync(string name)
        {
            return await Task.FromResult($"Hello {name}!");
        }
    }

    public class CadenceTests : IClassFixture<CadenceFixture>
    {
        private CadenceFixture  fixture;
        private CadenceClient   client;

        public CadenceTests(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DefaultDomain = "test-domain",
                LogLevel      = LogLevel.Info,
                CreateDomain  = true            // <-- this ensures that the default domain exists
            };

            // This starts/restarts the [nforgeio/cadence-dev] container for the first test
            // run in this class.  Subsequent tests run from the class will use the existing
            // container instance, saving time by not having to wait for Cadence and Cassandra
            // to spin up and be ready for business.
            //
            // The [keepOpen=true] parameter tells the fixture to let the container continue running
            // after all of the tests have completed.  This is useful for examining workflow histories
            // via the Cadence UX after the tests have completed.  You can view the Cadence portal at
            //
            //      http://localhost:8088
            //
            // You can pass [keepOpen=false] to have the fixture remove the container after the
            // test run if you wish.

            if (fixture.Start(settings, keepConnection: true, keepOpen: true) == TestFixtureStatus.Started)
            {
                // Register the test workflow and activity implementations
                // from this assembly and start the worker.

                client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                client.StartWorkerAsync("test-tasks").Wait();
            }

            this.fixture = fixture;
            this.client  = fixture.Client;
        }

        [Fact]
        public async Task HelloWorld()
        {
            var stub   = client.NewWorkflowStub<IHelloWorkflow>();
            var result = await stub.HelloAsync("Jeff");

            Assert.Equal("Hello Jeff!", result);
        }
    }
}
