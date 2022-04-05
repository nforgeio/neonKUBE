//-----------------------------------------------------------------------------
// FILE:	    QueueService.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.IO;
using Neon.Service;
using Neon.Xunit;

using NATS.Client;

using Xunit;

namespace TestNeonService
{
    /// <summary>
    /// <para>
    /// Implements a simple service that spins slowly, reading and writing messages
    /// for a NATS queue.  This expects the following environment variables:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>NATS_URI</b></term>
    ///     <description>
    ///     Specifies the URI for the NATS server.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NATS_QUEUE</b></term>
    ///     <description>
    ///     Identifies the queue where messages will be send and received.
    ///     </description>
    /// </item>
    /// </list>
    /// </summary>
    public class QueueService : NeonService
    {
        private string      natsServerUri;
        private string      natsQueue;
        private Task        sendTask;
        private Task        receiveTask;
        private IConnection nats;
        private bool        terminating;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public QueueService(string name, ServiceMap serviceMap = null)
            : base(name, serviceMap: serviceMap)
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Load the configuration environment variables, exiting with a
            // non-zero exit code if they don't exist.

            natsServerUri = Environment.Get("NATS_URI", string.Empty);

            if (string.IsNullOrEmpty(natsServerUri))
            {
                Log.LogCritical("Invalid configuration: [NATS_URI] environment variable is missing or invalid.");
                Exit(1);
            }

            natsQueue = GetEnvironmentVariable("NATS_QUEUE");

            if (string.IsNullOrEmpty(natsQueue))
            {
                Log.LogCritical("Invalid configuration: [NATS_QUEUE] environment variable is missing or invalid.");
                Exit(1);
            }

            // Connect to NATS.

            var connectionFactory = new ConnectionFactory();
            var natOptions        = ConnectionFactory.GetDefaultOptions();

            natOptions.Servers = new string[] { natsServerUri };

            nats = connectionFactory.CreateConnection(natOptions);

            // Start the service tasks

            sendTask    = Task.Run(async () => await SendTaskFunc());
            receiveTask = Task.Run(async () => await ReceiveTaskFunc());

            // Indicate that the service is running.

            await StartedAsync();

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();

            // We're going to dispose the NATS connection which will cause any 
            // pending or future NAT calls to fail and throw an exception.  This
            // will ultimately cause the tasks to exit.
            //
            // Note that we're setting [terminate=true] so the task exception
            // handlers can handle termination related exceptions by not logging
            // them and exiting the task.

            terminating = true;

            nats.Dispose();

            // Wait for the service task to exit and then indicate that the 
            // service has exited cleanly.

            await sendTask;
            Terminator.ReadyToExit();

            return 0;
        }

        /// <summary>
        /// Returns the number of messages sent.
        /// </summary>
        public int SentCount { get; private set; }

        /// <summary>
        /// Returns the number of messages received.
        /// </summary>
        public int ReceiveCount { get; private set; }

        /// <summary>
        /// Spins slowly sending NATs messages.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SendTaskFunc()
        {
            while (!Terminator.TerminateNow)
            {
                try
                {
                    nats.Publish(natsQueue, new byte[] { 0, 1, 2, 3, 4 });
                    SentCount++;

                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
                catch (Exception e)
                {
                    if (terminating)
                    {
                        break;
                    }
                    else
                    {
                        Log.LogError("SendTaskFunc", e);
                    }
                }
            }
        }

        /// <summary>
        /// Receives NATS messages.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ReceiveTaskFunc()
        {
            try
            {
                nats.SubscribeAsync(natsQueue,
                    (sender, args) =>
                    {
                        ReceiveCount++;
                    });
            }
            catch (Exception e)
            {
                if (!terminating)
                {
                    Log.LogError("ReceiveTaskFunc", e);
                }
            }

            await Task.CompletedTask;
        }
    }
}
