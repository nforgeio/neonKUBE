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
    [Trait(TestTrait.Category, TestTrait.Buggy)]
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
            TestHelper.ResetDocker(this.GetType());

            testWriter = new TestOutputWriter(outputHelper);

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

            if (fixture.Start(settings, reconnect: true, keepRunning: CadenceTestHelper.KeepCadenceServerOpen) == TestFixtureStatus.Started)
            {
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };

                // Setup a service for activity dependency injection testing if it doesn't
                // already exist.

                if (NeonHelper.ServiceContainer.GetService<ActivityDependency>() == null)
                {
                    NeonHelper.ServiceContainer.AddSingleton(typeof(ActivityDependency), new ActivityDependency() { Hello = "World!" });
                }

                // Auto register the test workflow and activity implementations.

                client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();

                // Start the worker.

                client.StartWorkerAsync(CadenceTestHelper.TaskList).Wait();
            }
            else
            {
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
            }
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
