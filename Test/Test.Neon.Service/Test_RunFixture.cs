//-----------------------------------------------------------------------------
// FILE:	    Test_RunFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace TestNeonService
{
    /// <summary>
    /// Tests the <see cref="TestHelper.RunFixture{T}"/> method.
    /// </summary>
    public class Test_RunFixture
    {
        //-----------------------------------------------------------
        // The test class.

        public class TestClass : IClassFixture<ComposedFixture>, IDisposable
        {
            private ServiceMap                          serviceMap;
            private ComposedFixture                     composedFixture;
            private NeonServiceFixture<WebService>      webServiceFixture;
            private NeonServiceFixture<RelayService>    relayServiceFixture;

            public TestClass(ComposedFixture fixture, string arg)
            {
                Test_RunFixture.argValue = arg;

                this.composedFixture = fixture;
                this.serviceMap      = CreateServiceMap();

                composedFixture.Start(
                    () =>
                    {
                        composedFixture.AddServiceFixture<WebService>("web-service", new NeonServiceFixture<WebService>(), () => CreateWebService());
                        composedFixture.AddServiceFixture<RelayService>("relay-service", new NeonServiceFixture<RelayService>(), () => CreateRelayService());
                    });

                this.webServiceFixture   = (NeonServiceFixture<WebService>)composedFixture["web-service"];
                this.relayServiceFixture = (NeonServiceFixture<RelayService>)composedFixture["relay-service"];

                Assert.True(webServiceFixture.IsRunning);
                Assert.True(relayServiceFixture.IsRunning);
            }

            public void Dispose()
            {
                Test_RunFixture.disposeWasCalled = true;
            }

            /// <summary>
            /// Returns the service map.
            /// </summary>
            private ServiceMap CreateServiceMap()
            {
                var serviceMap = new ServiceMap();

                //---------------------------------------------
                // web-service:

                var description = new ServiceDescription()
                {
                    Name    = "web-service",
                    Address = "127.0.0.10"
                };

                description.Endpoints.Add(
                    new ServiceEndpoint()
                    {
                        Protocol   = ServiceEndpointProtocol.Http,
                        PathPrefix = "/",
                        Port       = 666
                    });

                serviceMap.Add(description);

                //---------------------------------------------
                // relay-service:

                description = new ServiceDescription()
                {
                    Name    = "relay-service",
                    Address = "127.0.0.10"
                };

                description.Endpoints.Add(
                    new ServiceEndpoint()
                    {
                        Protocol   = ServiceEndpointProtocol.Http,
                        PathPrefix = "/",
                        Port       = 777
                    });

                serviceMap.Add(description);

                return serviceMap;
            }

            /// <summary>
            /// Creates a <see cref="WebService"/> instance.
            /// </summary>
            /// <returns>The service instance.</returns>
            private WebService CreateWebService()
            {
                var service = new WebService(CreateServiceMap(), "web-service");

                service.SetEnvironmentVariable("WEB_RESULT", "Hello World!");

                return service;
            }

            /// <summary>
            /// Creates a <see cref="RelayService"/> instance.
            /// </summary>
            /// <returns>The service instance.</returns>
            private RelayService CreateRelayService()
            {
                return new RelayService(CreateServiceMap(), "relay-service");
            }

            /// <summary>
            /// The test method.
            /// </summary>
            public void Run()
            {
                Test_RunFixture.runWasCalled = true;

                // Verify that the services look OK and then return.

                using (var client = new HttpClient())
                {
                    // Both the [web-service] and [relay-services] should be running.
                    // We'll first query the [web-service] directly and verify that
                    // we get the expected response.

                    var webServiceDescription = serviceMap["web-service"];

                    Assert.Equal("Hello World!", client.GetStringAsync(webServiceDescription.Endpoints.Default.Uri).Result);

                    // Now query the [relay-service] which calls the [web-service]
                    // and verify.

                    var relayServiceDescription = serviceMap["relay-service"];

                    Assert.Equal("Hello World!", client.GetStringAsync(relayServiceDescription.Endpoints.Default.Uri).Result);
                }
            }
        }

        //-----------------------------------------------------------
        // Static members

        private static string   argValue         = null;
        private static bool     runWasCalled     = false;
        private static bool     disposeWasCalled = false;

        //-----------------------------------------------------------
        // Instance members

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void Run()
        {
            runWasCalled     = false;
            disposeWasCalled = false;

            TestHelper.RunFixture<TestClass>("Hello World!");

            Assert.Equal("Hello World!", Test_RunFixture.argValue);
            Assert.True(runWasCalled);
            Assert.True(disposeWasCalled);
        }
    }
}
