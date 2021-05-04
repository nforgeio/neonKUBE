//-----------------------------------------------------------------------------
// FILE:	    Test_Dependencies.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Service;
using Neon.Tasks;
using Neon.Xunit;

using Prometheus;

using Xunit;

namespace TestNeonService
{
    [Trait(TestTrait.Area, TestArea.NeonService)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_Dependencies : IDisposable
    {
        //---------------------------------------------------------------------
        // Service implementation

        public class TestService : NeonService
        {
            public TestService()
                : base("test-service")
            {
            }

            /// <summary>
            /// Signals that the service has started running.
            /// </summary>
            public AsyncAutoResetEvent Running { get; set; } = new AsyncAutoResetEvent();

            /// <summary>
            /// Implements the test service.
            /// </summary>
            /// <returns>The service exit code.</returns>
            protected override async Task<int> OnRunAsync()
            {
                Running.Set();

                return await Task.FromResult(0);
            }
        }

        //---------------------------------------------------------------------
        // Tests

        public void Dispose()
        {
            // Ensure that any dependency related environment variables are removed
            // after each test.

            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_URIS", null);
            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_TIMEOUT_SECONDS", null);
            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_WAIT_SECONDS", null);
        }

        [Fact]
        public async Task Wait_Explicit()
        {
            // Verify that a service will honor the wait time specified in code.

            var service   = new TestService();
            var stopWatch = new Stopwatch();
            var waitTime  = TimeSpan.FromSeconds(5);

            service.Dependencies.Wait = waitTime;

            stopWatch.Start();

            var runTask = service.RunAsync();

            await service.Running.WaitAsync();
            Assert.True(stopWatch.Elapsed >= waitTime);

            Assert.Equal(0, await runTask);
        }

        [Fact]
        public async Task Wait_EnvironmentVar()
        {
            // Verify that a service will honor the wait time specified as
            // an environment variable.

            var waitTime = TimeSpan.FromSeconds(5);

            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_WAIT_SECONDS", waitTime.TotalSeconds.ToString());

            var service   = new TestService();
            var stopWatch = new Stopwatch();

            Assert.Equal(waitTime, service.Dependencies.Wait);

            stopWatch.Start();

            var runTask = service.RunAsync();

            await service.Running.WaitAsync();
            Assert.True(stopWatch.Elapsed >= waitTime);

            Assert.Equal(0, await runTask);
        }

        [Fact]
        public async Task Wait_BadEnvironmentVar()
        {
            // Verify that a service ignores an invalid environment variable.

            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_WAIT_SECONDS", "NOT-A-DOUBLE");

            var service = new TestService();

            var stopWatch = new Stopwatch();
            var runTask = service.RunAsync();

            Assert.Equal(0, await runTask);
        }

        [Fact]
        public async Task Dependencies_Explicit()
        {
            // Verify that a service will wait for service dependencies for each
            // supported URI scheme set explicitly.

            var service    = new TestService();
            var stopWatch  = new Stopwatch();
            var startDelay = 2.0;
            var port0      = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var port1      = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var port2      = NetHelper.GetUnusedIpPort(IPAddress.Loopback);

            service.Dependencies.Uris.Add(new Uri($"http://127.0.0.1:{port0}"));
            service.Dependencies.Uris.Add(new Uri($"https://127.0.0.1:{port1}"));
            service.Dependencies.Uris.Add(new Uri($"tcp://127.0.0.1:{port2}"));

            stopWatch.Start();

            var runTask = service.RunAsync();

            // Start each simulated service dependency one at a time after waiting
            // [startDelay] seconds for each and then verify that the service waited
            // at least [3*startDelay] to start.
            //
            // Note that it doesn't matter that we're emulating all of the services
            // as an [HttpListener] because the service is only verifying that it 
            // can establish a socket connection; its not actually submitting any
            // requests.

            var listener0 = new HttpListener();
            var listener1 = new HttpListener();
            var listener2 = new HttpListener();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener0.Prefixes.Add($"http://127.0.0.1:{port0}/");
            listener0.Start();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener1.Prefixes.Add($"http://127.0.0.1:{port1}/");
            listener1.Start();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener2.Prefixes.Add($"http://127.0.0.1:{port2}/");
            listener2.Start();

            try
            {
                await service.Running.WaitAsync();
                Assert.True(stopWatch.Elapsed >= TimeSpan.FromSeconds(startDelay * 3));

                Assert.Equal(0, await runTask);
            }
            finally
            {
                listener0.Stop();
                listener1.Stop();
                listener2.Stop();
            }
        }

        [Fact]
        public async Task Dependencies_EnvironmentVar()
        {
            // Verify that a service will wait for service dependencies for each
            // supported URI scheme set via an environment variable.

            var port0 = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var port1 = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var port2 = NetHelper.GetUnusedIpPort(IPAddress.Loopback);

            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_URIS", $"http://127.0.0.1:{port0}; https://127.0.0.1:{port1}/; tcp://127.0.0.1:{port2}/");

            var service    = new TestService();
            var stopWatch  = new Stopwatch();
            var startDelay = 2.0;

            Assert.Equal($"http://127.0.0.1:{port0}/", service.Dependencies.Uris[0].ToString());
            Assert.Equal($"https://127.0.0.1:{port1}/", service.Dependencies.Uris[1].ToString());
            Assert.Equal($"tcp://127.0.0.1:{port2}/", service.Dependencies.Uris[2].ToString());

            stopWatch.Start();

            var runTask = service.RunAsync();

            // Start each simulated service dependency one at a time after waiting
            // [startDelay] seconds for each and then verify that the service waited
            // at least [3*startDelay] to start.
            //
            // Note that it doesn't matter that we're emulating all of the services
            // as an [HttpListener] because the service is only verifying that it 
            // can establish a socket connection; its not actually submitting any
            // requests.

            var listener0 = new HttpListener();
            var listener1 = new HttpListener();
            var listener2 = new HttpListener();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener0.Prefixes.Add($"http://127.0.0.1:{port0}/");
            listener0.Start();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener1.Prefixes.Add($"http://127.0.0.1:{port1}/");
            listener1.Start();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener2.Prefixes.Add($"http://127.0.0.1:{port2}/");
            listener2.Start();

            try
            {
                await service.Running.WaitAsync();
                Assert.True(stopWatch.Elapsed >= TimeSpan.FromSeconds(startDelay * 3));

                Assert.Equal(0, await runTask);
            }
            finally
            {
                listener0.Stop();
                listener1.Stop();
                listener2.Stop();
            }
        }

        [Fact]
        public async Task Dependencies_BadEnvironmentVar()
        {
            // Verify that a service will wait for service dependencies for each
            // supported URI scheme set via an environment variable while 
            // ignorning an invalid URI and unsupported URI scheme.

            var port0 = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var port1 = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var port2 = NetHelper.GetUnusedIpPort(IPAddress.Loopback);

            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_URIS", $"http://127.0.0.1:{port0}; https://127.0.0.1:{port1}/; tcp://127.0.0.1:{port2}/; mailto://notsupported:80; BAD-URI");

            var service    = new TestService();
            var stopWatch  = new Stopwatch();
            var startDelay = 2.0;

            Assert.Equal($"http://127.0.0.1:{port0}/", service.Dependencies.Uris[0].ToString());
            Assert.Equal($"https://127.0.0.1:{port1}/", service.Dependencies.Uris[1].ToString());
            Assert.Equal($"tcp://127.0.0.1:{port2}/", service.Dependencies.Uris[2].ToString());

            stopWatch.Start();

            var runTask = service.RunAsync();

            // Start each simulated service dependency one at a time after waiting
            // [startDelay] seconds for each and then verify that the service waited
            // at least [3*startDelay] to start.
            //
            // Note that it doesn't matter that we're emulating all of the services
            // as an [HttpListener] because the service is only verifying that it 
            // can establish a socket connection; its not actually submitting any
            // requests.

            var listener0 = new HttpListener();
            var listener1 = new HttpListener();
            var listener2 = new HttpListener();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener0.Prefixes.Add($"http://127.0.0.1:{port0}/");
            listener0.Start();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener1.Prefixes.Add($"http://127.0.0.1:{port1}/");
            listener1.Start();

            await Task.Delay(TimeSpan.FromSeconds(startDelay));
            listener2.Prefixes.Add($"http://127.0.0.1:{port2}/");
            listener2.Start();

            try
            {
                await service.Running.WaitAsync();
                Assert.True(stopWatch.Elapsed >= TimeSpan.FromSeconds(startDelay * 3));

                Assert.Equal(0, await runTask);
            }
            finally
            {
                listener0.Stop();
                listener1.Stop();
                listener2.Stop();
            }
        }

        [Fact]
        public async Task Dependencies_Timeout_Explicit()
        {
            // Verify that a service will timeout waiting for a service
            // that is never available when the timeout is configured
            // explicitly.

            var port    = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var timeout = TimeSpan.FromSeconds(2);

            var service   = new TestService();
            var stopWatch = new Stopwatch();

            service.Dependencies.Uris.Add(new Uri(($"http://127.0.0.1:{port}")));
            service.Dependencies.Timeout = timeout;

            stopWatch.Start();

            Assert.Equal(1, await service.RunAsync());
            Assert.True(stopWatch.Elapsed > timeout);
        }

        [Fact]
        public async Task Dependencies_Timeout_EnvironmentVar()
        {
            // Verify that a service will timeout waiting for a service
            // that is never available when the timeout is configured
            // via an environment variable.

            var port    = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var timeout = TimeSpan.FromSeconds(2);

            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_URIS", $"http://127.0.0.1:{port};");
            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_TIMEOUT_SECONDS", timeout.TotalSeconds.ToString());

            var service   = new TestService();
            var stopWatch = new Stopwatch();

            Assert.Single(service.Dependencies.Uris);
            Assert.Equal($"http://127.0.0.1:{port}/", service.Dependencies.Uris[0].ToString());
            Assert.Equal(timeout, service.Dependencies.Timeout);

            stopWatch.Start();

            Assert.Equal(1, await service.RunAsync());
            Assert.True(stopWatch.Elapsed > timeout);
        }

        [Fact]
        public async Task Dependencies_Timeout_BadEnvironmentVar()
        {
            // Verify that a service will ignore an invalid timeout specified
            // as an environment variable.

            var port    = NetHelper.GetUnusedIpPort(IPAddress.Loopback);
            var timeout = TimeSpan.FromSeconds(2);

            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_URIS", $"http://127.0.0.1:{port};");
            Environment.SetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_TIMEOUT_SECONDS", "BAD-VALUE");

            var service   = new TestService();
            var stopWatch = new Stopwatch();

            Assert.Single(service.Dependencies.Uris);
            Assert.Equal($"http://127.0.0.1:{port}/", service.Dependencies.Uris[0].ToString());
            Assert.Equal(new ServiceDependencies().Timeout, service.Dependencies.Timeout);

            service.Dependencies.TestTimeout = timeout;

            stopWatch.Start();

            Assert.Equal(1, await service.RunAsync());
            Assert.True(stopWatch.Elapsed > timeout);
        }
    }
}
