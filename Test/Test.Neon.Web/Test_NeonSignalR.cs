//-----------------------------------------------------------------------------
// FILE:	    Test_WebService.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#if !NETCOREAPP3_1

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Service;
using Neon.Web.SignalR;
using Neon.Xunit;

using Xunit;
using NATS.Client;

namespace Test.Neon.SignalR
{
    /// <summary>
    /// Demonstrates how to test the <see cref="WebService"/> that has a single
    /// HTTP endpoint and that also exercises environment variable and file based 
    /// configuration.
    /// </summary>
    [Trait(TestTrait.Category, TestArea.NeonService)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_WebService : IClassFixture<ComposedFixture>
    {
        private ComposedFixture                     composedFixture;
        private NatsFixture                         natsFixture;
        private NeonServiceFixture<WebService>      web0;
        private NeonServiceFixture<WebService>      web1;

        private HubConnection connection;
        private HubConnection secondConnection;
        private HubConnection thirdConnection;
        private HubConnection fourthConnection;

        public Test_WebService(ComposedFixture fixture)
        {
            TestHelper.ResetDocker(this.GetType());

            this.composedFixture = fixture;

            composedFixture.Start(
                () =>
                {
                    composedFixture.AddFixture("nats", new NatsFixture(),
                        natsFixture =>
                        {
                            natsFixture.StartAsComposed();
                        });

                    composedFixture.AddServiceFixture("web-0", new NeonServiceFixture<WebService>(), () => CreateService("web-0"));
                    composedFixture.AddServiceFixture("web-1", new NeonServiceFixture<WebService>(), () => CreateService("web-1"));
                });

            this.natsFixture = (NatsFixture)composedFixture["nats"];
            this.web0        = (NeonServiceFixture<WebService>)composedFixture["web-0"];
            this.web1        = (NeonServiceFixture<WebService>)composedFixture["web-1"];

            connection       = CreateConnection(web0.Service.ServiceMap["web-0"].Endpoints.Default.Uri + "echo", HttpTransportType.WebSockets, userName: "userA");
            secondConnection = CreateConnection(web1.Service.ServiceMap["web-1"].Endpoints.Default.Uri + "echo", HttpTransportType.WebSockets, userName: "userA");
            thirdConnection  = CreateConnection(web1.Service.ServiceMap["web-0"].Endpoints.Default.Uri + "echo", HttpTransportType.WebSockets, userName: "userB");
            fourthConnection = CreateConnection(web1.Service.ServiceMap["web-1"].Endpoints.Default.Uri + "echo", HttpTransportType.WebSockets, userName: "userC");
        }

        /// <summary>
        /// Returns the service map.
        /// </summary>
        private ServiceMap CreateServiceMap()
        {
            var serviceMap = new ServiceMap();

            var description = new ServiceDescription()
            {
                Name    = "web-0",
                Address = "127.0.0.10"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol   = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port       = 8080
                });

            serviceMap.Add(description);

            description = new ServiceDescription()
            {
                Name    = "web-1",
                Address = "127.0.0.11"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol   = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port       = 8081
                });

            serviceMap.Add(description);

            return serviceMap;
        }

        /// <summary>
        /// Creates a <see cref="WebService"/> instance.
        /// </summary>
        /// <returns>The service instance.</returns>
        private WebService CreateService(string name)
        {
            var service = new WebService(name, serviceMap: CreateServiceMap());

            service.SetEnvironmentVariable("NATS_URI", NatsFixture.ConnectionUri);
            service.SetEnvironmentVariable("NATS_QUEUE", "test");

            return service;
        }

        [Fact]
        public void AddNeonNats()
        {
            var natsServerUri = NatsFixture.ConnectionUri;

            var connectionFactory = new ConnectionFactory();
            var options           = ConnectionFactory.GetDefaultOptions();

            options.Servers = new string[] { natsServerUri };

            var services = new ServiceCollection();

            var exception = Record.Exception(() => services.AddSignalR().AddNeonNats(connectionFactory.CreateConnection()));
            Assert.Null(exception);
        }

        [Fact]
        public void AddNeonNatsWithIlogger()
        {
            var natsServerUri = NatsFixture.ConnectionUri;

            var connectionFactory = new ConnectionFactory();
            var options           = ConnectionFactory.GetDefaultOptions();

            options.Servers = new string[] { natsServerUri };

            var services = new ServiceCollection();
            services.AddLogging();

            var exception = Record.Exception(() => services.AddSignalR().AddNeonNats(connectionFactory.CreateConnection()));
            Assert.Null(exception);
        }

        [Fact]
        public void AddNeonNatsWithINeonlogger()
        {
            var natsServerUri = NatsFixture.ConnectionUri;

            var connectionFactory = new ConnectionFactory();
            var options           = ConnectionFactory.GetDefaultOptions();

            options.Servers = new string[] { natsServerUri };

            var services = new ServiceCollection();
            var logger   = new global::Neon.Diagnostics.LogManager().GetLogger();
            
            services.AddSingleton(logger);

            var exception = Record.Exception(() => services.AddSignalR().AddNeonNats(connectionFactory.CreateConnection()));
            Assert.Null(exception);
        }

        [Fact]
        public async Task CanSendAndReceiveUserMessagesFromMultipleConnectionsWithSameUser()
        {
            var tcs = new TaskCompletionSource<string>();

            connection.On<string>("Echo", message => tcs.SetResult(message));

            var tcs2 = new TaskCompletionSource<string>();

            secondConnection.On<string>("Echo", message => tcs2.SetResult(message));

            await CheckConnectionsAsync();
            await connection.InvokeAsync("EchoUser", "userA", "Hello, World!");

            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs.Task));
            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs2.Task));
        }

        [Fact]
        public async Task CanInvokeMethodWithoutOptionalParams()
        {
            var tcs = new TaskCompletionSource<string>();

            connection.On<string>("Echo", message => tcs.SetResult(message));

            var tcs2 = new TaskCompletionSource<string>();

            secondConnection.On<string>("Echo", message => tcs2.SetResult(message));

            await CheckConnectionsAsync();
            await connection.InvokeAsync("SayHello", null);

            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs.Task));
            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs2.Task));
        }

        [Fact]
        public async Task CanSendAndReceiveUserMessagesToOtherUsers()
        {
            var tcs = new TaskCompletionSource<string>();

            connection.On<string>("Echo", message => tcs.SetResult(message));

            var tcs2 = new TaskCompletionSource<string>();

            secondConnection.On<string>("Echo", message => tcs2.SetResult(message));

            var tcs3 = new TaskCompletionSource<string>();

            thirdConnection.On<string>("Echo", message => tcs3.SetResult(message));

            var tcs4 = new TaskCompletionSource<string>();

            fourthConnection.On<string>("Echo", message => tcs4.SetResult(message));

            await CheckConnectionsAsync();

            await connection.InvokeAsync("EchoUser", "userA", "Hello, World!");

            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs.Task));
            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs2.Task));
            Assert.Null(await AwaitWithTimeoutAsync<string>(tcs3.Task, throwOnTimeout: false));
            Assert.Null(await AwaitWithTimeoutAsync<string>(tcs4.Task, throwOnTimeout: false));

            tcs  = new TaskCompletionSource<string>();
            tcs2 = new TaskCompletionSource<string>();
            tcs3 = new TaskCompletionSource<string>();
            tcs4 = new TaskCompletionSource<string>();

            await connection.InvokeAsync("EchoUser", "userC", "Hello, World!");

            Assert.Null(await AwaitWithTimeoutAsync<string>(tcs.Task, throwOnTimeout: false));
            Assert.Null(await AwaitWithTimeoutAsync<string>(tcs2.Task, throwOnTimeout: false));
            Assert.Null(await AwaitWithTimeoutAsync<string>(tcs3.Task, throwOnTimeout: false));
            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs4.Task));

        }

        [Fact]
        public async Task HubConnectionCanSendAndReceiveGroupMessages()
        {
            var tcs = new TaskCompletionSource<string>();

            connection.On<string>("Echo", message => tcs.SetResult(message));

            var tcs2 = new TaskCompletionSource<string>();

            secondConnection.On<string>("Echo", message => tcs2.SetResult(message));

            var groupName = $"HubConnectionCanSendAndReceiveGroupMessages_{Guid.NewGuid():N}";

            await CheckConnectionsAsync();
            await connection.InvokeAsync("AddSelfToGroup", groupName);
            await secondConnection.InvokeAsync("AddSelfToGroup", groupName);

            await connection.InvokeAsync("EchoGroup", groupName, "Hello, World!");

            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs.Task));
            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs2.Task));
        }

        [Fact]
        public async Task HubConnectionCanUnsubscribeFromGroupMessages()
        {
            var tcs = new TaskCompletionSource<string>();

            connection.On<string>("Echo", message => tcs.SetResult(message));

            var tcs2 = new TaskCompletionSource<string>();

            secondConnection.On<string>("Echo", message => tcs2.SetResult(message));

            var groupName = $"HubConnectionCanUnsubscribeFromGroupMessages_{Guid.NewGuid():N}";

            await CheckConnectionsAsync();
            await connection.InvokeAsync("AddSelfToGroup", groupName);
            await secondConnection.InvokeAsync("AddSelfToGroup", groupName);
            await connection.InvokeAsync("EchoGroup", groupName, "Hello, World!");

            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs.Task));
            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs2.Task));

            tcs  = new TaskCompletionSource<string>();
            tcs2 = new TaskCompletionSource<string>();

            await secondConnection.InvokeAsync("RemoveSelfFromGroup", groupName);
            await connection.InvokeAsync("EchoGroup", groupName, "Hello, World!");

            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs.Task));
            Assert.Null(await AwaitWithTimeoutAsync<string>(tcs2.Task, throwOnTimeout: false));
        }

        [Fact]
        public async Task HubConnectionCanAddUserToGroup()
        {
            var tcs = new TaskCompletionSource<string>();

            connection.On<string>("Echo", message => tcs.SetResult(message));

            var tcs2 = new TaskCompletionSource<string>();

            secondConnection.On<string>("Echo", message => tcs2.SetResult(message));

            var groupName = $"HubConnectionCanAddUserToGroup_{Guid.NewGuid():N}";

            await CheckConnectionsAsync();
            await secondConnection.InvokeAsync("AddUserToGroup", connection.ConnectionId, groupName);
            await connection.InvokeAsync("EchoGroup", groupName, "Hello, World!");

            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs.Task));
        }

        [Fact]
        public async Task HubConnectionCanRemoveUserFromGroup()
        {
            var tcs = new TaskCompletionSource<string>();

            connection.On<string>("Echo", message => tcs.SetResult(message));

            var tcs2 = new TaskCompletionSource<string>();

            secondConnection.On<string>("Echo", message => tcs2.SetResult(message));

            var groupName = $"HubConnectionCanRemoveUserFromGroup_{Guid.NewGuid():N}";

            await CheckConnectionsAsync();
            await secondConnection.InvokeAsync("AddUserToGroup", connection.ConnectionId, groupName);
            await connection.InvokeAsync("EchoGroup", groupName, "Hello, World!");

            Assert.Equal("Hello, World!", await AwaitWithTimeoutAsync<string>(tcs.Task));

            tcs = new TaskCompletionSource<string>();

            await secondConnection.InvokeAsync("RemoveUserFromGroup", connection.ConnectionId, groupName);
            await secondConnection.InvokeAsync("EchoGroup", groupName, "Hello, World!");

            Assert.Null(await AwaitWithTimeoutAsync<string>(tcs2.Task, throwOnTimeout: false));
        }

        private async Task CheckConnectionsAsync()
        {
            if (connection.State != HubConnectionState.Connected)
            {
                await connection.StartAsync();
            }

            if (secondConnection.State != HubConnectionState.Connected)
            {
                await secondConnection.StartAsync();
            }

            if (thirdConnection.State != HubConnectionState.Connected)
            {
                await thirdConnection.StartAsync();
            }

            if (fourthConnection.State != HubConnectionState.Connected)
            {
                await fourthConnection.StartAsync();
            }
        }

        private static HubConnection CreateConnection(string url, HttpTransportType transportType, string userName = null)
        {
            var hubConnectionBuilder = new HubConnectionBuilder()
                .WithAutomaticReconnect()
                .WithUrl(url, transportType, httpConnectionOptions =>
                {
                    httpConnectionOptions.CloseTimeout = TimeSpan.MaxValue;
                    if (!string.IsNullOrEmpty(userName))
                    {
                        httpConnectionOptions.Headers["UserName"] = userName;
                    }
                });

            hubConnectionBuilder.Services.AddSingleton(new MessagePackHubProtocol());

            var connection = hubConnectionBuilder.Build();

            connection.KeepAliveInterval = TimeSpan.FromSeconds(5);
            connection.ServerTimeout     = TimeSpan.FromSeconds(300);

            return connection;
        }

        private async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task, int timeout = 500, bool throwOnTimeout = true)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                return await task;
            }
            
            if (throwOnTimeout)
            {
                throw new TimeoutException("Operation timed out.");
            }

            return default;
        }
    }
}

#endif
