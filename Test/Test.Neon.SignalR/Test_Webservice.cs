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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Service;
using Neon.SignalR;
using Neon.Xunit;

using Xunit;

namespace TestNeonSignalR
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
        private ComposedFixture composedFixture;
        private NatsFixture natsFixture;
        private NeonServiceFixture<WebService> web0;
        private NeonServiceFixture<WebService> web1;

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
        }

        /// <summary>
        /// Returns the service map.
        /// </summary>
        private ServiceMap CreateServiceMap()
        {
            var serviceMap = new ServiceMap();

            var description = new ServiceDescription()
            {
                Name = "web-0",
                Address = "127.0.0.10"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port = 8080
                });


            serviceMap.Add(description);

            description = new ServiceDescription()
            {
                Name = "web-1",
                Address = "127.0.0.11"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port = 8081
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
        public async Task CanSendAndReceiveUserMessagesFromMultipleConnectionsWithSameUser()
        {
            var protocol         = HubProtocolHelpers.NewtonsoftJsonHubProtocol;
            var connection       = CreateConnection(web0.Service.ServiceMap["web-0"].Endpoints.Default.Uri + "echo", HttpTransportType.WebSockets, protocol, userName: "userA");
            var secondConnection = CreateConnection(web1.Service.ServiceMap["web-1"].Endpoints.Default.Uri + "echo", HttpTransportType.WebSockets, protocol, userName: "userA");

            var tcs = new TaskCompletionSource<string>();
            connection.On<string>("Echo", message => tcs.TrySetResult(message));
            var tcs2 = new TaskCompletionSource<string>();
            secondConnection.On<string>("Echo", message => tcs2.TrySetResult(message));

            await secondConnection.StartAsync();
            await connection.StartAsync();
            await connection.InvokeAsync("EchoUser", "userA", "Hello, World!");

            Assert.Equal("Hello, World!", await tcs.Task);
            Assert.Equal("Hello, World!", await tcs2.Task);

            await connection.DisposeAsync();
            await secondConnection.DisposeAsync();
        }

        [Fact]
        public async Task HubConnectionCanSendAndReceiveGroupMessages()
        {
            var protocol         = HubProtocolHelpers.NewtonsoftJsonHubProtocol;
            var connection       = CreateConnection(web0.Service.ServiceMap["web-0"].Endpoints.Default.Uri + "echo", HttpTransportType.WebSockets, protocol, userName: "userA");
            var secondConnection = CreateConnection(web1.Service.ServiceMap["web-1"].Endpoints.Default.Uri + "echo", HttpTransportType.WebSockets, protocol, userName: "userA");

            var tcs = new TaskCompletionSource<string>();
            connection.On<string>("Echo", message => tcs.TrySetResult(message));
            var tcs2 = new TaskCompletionSource<string>();
            secondConnection.On<string>("Echo", message => tcs2.TrySetResult(message));
            
            var groupName = $"TestGroup_{HttpTransportType.WebSockets}_{protocol.Name}_{Guid.NewGuid()}";

            await secondConnection.StartAsync();
            await connection.StartAsync();

            await connection.InvokeAsync("AddSelfToGroup", groupName);
            await secondConnection.InvokeAsync("AddSelfToGroup", groupName);

            await connection.InvokeAsync("EchoGroup", groupName, "Hello, World!");

            Assert.Equal("Hello, World!", await tcs.Task);
            Assert.Equal("Hello, World!", await tcs2.Task);

            await connection.DisposeAsync();
            await secondConnection.DisposeAsync();
        }

        private static HubConnection CreateConnection(string url, HttpTransportType transportType, IHubProtocol protocol, string userName = null)
        {
            var hubConnectionBuilder = new HubConnectionBuilder()
                .WithUrl(url, transportType, httpConnectionOptions =>
                {
                    if (!string.IsNullOrEmpty(userName))
                    {
                        httpConnectionOptions.Headers["UserName"] = userName;
                    }
                });

            hubConnectionBuilder.Services.AddSingleton(protocol);

            return hubConnectionBuilder.Build();
        }
    }
}
