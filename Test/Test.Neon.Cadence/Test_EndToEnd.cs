//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace TestCadence
{
    [Trait(TestTrait.Category, TestArea.NeonCadence)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_EndToEnd : IClassFixture<CadenceFixture>, IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        private class TestException : Exception
        {
            public TestException(string message)
                : base(message)
            {
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const int maxWaitSeconds = 10;

        private static readonly TimeSpan allowedVariation = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan workflowTimeout  = TimeSpan.FromSeconds(20);

        private CadenceFixture      fixture;
        private TestOutputWriter    testWriter;
        private CadenceClient       client;
        private HttpClient          proxyClient;

        public Test_EndToEnd(CadenceFixture fixture, ITestOutputHelper outputHelper)
        {
NeonHelper.DebugLogPath = @"C:\Temp\log.txt";
NeonHelper.ClearDebugLog();
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 0");
            TestHelper.ResetDocker(this.GetType());
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 0A");

            testWriter = new TestOutputWriter(outputHelper);
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 0B");

            // Initialize the Cadence fixture.

            var settings = new CadenceSettings()
            {
                DefaultDomain          = CadenceFixture.DefaultDomain,
                LogLevel               = CadenceTestHelper.LogLevel,
                CreateDomain           = true,
                Debug                  = CadenceTestHelper.Debug,
                DebugPrelaunched       = CadenceTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats = CadenceTestHelper.DebugDisableHeartbeats,
                ClientIdentity         = CadenceTestHelper.ClientIdentity
            };
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 0C");

            if (fixture.Start(settings, reconnect: true, keepRunning: CadenceTestHelper.KeepCadenceServerOpen) == TestFixtureStatus.Started)
            {
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 1");
                this.fixture     = fixture;
                this.client      = fixture.Client;
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 1A");
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 1B");

                // Setup a service for activity dependency injection testing if it doesn't
                // already exist.

NeonHelper.LogDebug("Test_EndToEnd::Constructor: 1C");
                if (NeonHelper.ServiceContainer.GetService<ActivityDependency>() == null)
                {
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 1D");
                    NeonHelper.ServiceContainer.AddSingleton(typeof(ActivityDependency), new ActivityDependency() { Hello = "World!" });
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 1E");
                }
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 2");

                // Auto register the test workflow and activity implementations.

                client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 3");

                // Start the worker.

                client.StartWorkerAsync(CadenceTestHelper.TaskList).Wait();
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 4");
            }
            else
            {
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 5");
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 6");
            }
NeonHelper.LogDebug("Test_EndToEnd::Constructor: 7");
        }

        public void Dispose()
        {
            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }
        }
    }
}
