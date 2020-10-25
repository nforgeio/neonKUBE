//-----------------------------------------------------------------------------
// FILE:	    ComplexService.cs
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

using Xunit;

namespace TestNeonService
{
    /// <summary>
    /// Startup class for <see cref="ComplexService"/>.
    /// </summary>
    public class ComplexServiceStartup
    {
        private ComplexService service;

        public ComplexServiceStartup(IConfiguration configuration, ComplexService service)
        {
            this.Configuration = configuration;
            this.service       = service;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Forward all requests to the parent service to have them
            // handled there.

            app.Run(async context => await service.OnWebRequest(context));
        }
    }

    /// <summary>
    /// Implements a somewhat complex service that services an HTTP endpoint, and also
    /// has a <see cref="Task"/> and <see cref="Thread"/> running in the background.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service demonstrates how to deploy a service with an ASP.NET endpoint that
    /// uses environment variables or a configuration file to specify the string
    /// returned by the endpoint.  This also demonstrates how a service can have
    /// and internal worker thread and/or task.
    /// </para>
    /// <para>
    /// The service looks for the <b>WEB_RESULT</b> environment variable and
    /// if present, will return the value as the endpoint response text.  Otherwise,
    /// the service will look for a configuration file at the logical path
    /// <b>/etc/complex/response</b> and return its contents of present.  If neither
    /// the environment variable or file are present, the endpoint will return
    /// <b>UNCONFIGURED</b>.
    /// </para>
    /// <para>
    /// We'll use these settings to exercise the <see cref="NeonService"/> logical
    /// configuration capabilities.
    /// </para>
    /// </remarks>
    public class ComplexService : NeonService
    {
        private IWebHost    webHost;
        private Thread      thread;
        private Task        task;
        private string      responseText;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceMap">The service map.</param>
        /// <param name="name">The service name.</param>
        public ComplexService(ServiceMap serviceMap, string name)
            : base(serviceMap, name, ThisAssembly.Git.Branch, ThisAssembly.Git.Commit, ThisAssembly.Git.IsDirty)
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // Dispose web host if it's still running.

            if (webHost != null)
            {
                webHost.Dispose();
                webHost = null;
            }
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Read the configuration environment variable or file to initialize
            // endpoint response text.

            responseText = "UNCONFIGURED";

            var resultVar = GetEnvironmentVariable("WEB_RESULT");

            if (resultVar != null)
            {
                responseText = resultVar;
            }
            else
            {
                var configPath = GetConfigFilePath("/etc/complex/response");

                if (configPath != null && File.Exists(configPath))
                {
                    responseText = File.ReadAllText(configPath);
                }
            }

            // Start the web service.

            var endpoint = Description.Endpoints.Default;

            webHost = new WebHostBuilder()
                .UseStartup<ComplexServiceStartup>()
                .UseKestrel(options => options.Listen(IPAddress.Any, endpoint.Port))
                .ConfigureServices(services => services.AddSingleton(typeof(ComplexService), this))
                .Build();

            webHost.Start();

            // Start the worker thread.

            thread = new Thread(new ThreadStart(ThreadFunc));
            thread.Start();

            // Start the service task 

            task = Task.Run(async () => await TaskFunc());

            // Indicate that the service is ready for business.

            await SetRunningAsync();

            // Wait for the process terminator to signal that the service is stopping.

            await Terminator.StopEvent.WaitAsync();

            // Wait for the service thread and task to exit.

            thread.Join();
            await task;

            // Return the exit code specified by the configuration.

            return await Task.FromResult(0);
        }

        /// <summary>
        /// Handles web requests.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task OnWebRequest(HttpContext context)
        {
            await context.Response.WriteAsync(responseText);
        }

        /// <summary>
        /// Demonstrates how a service thread can be signalled to terminate.
        /// </summary>
        private void ThreadFunc()
        {
            // Terminating threads are a bit tricky.  The only acceptable way
            // to do this by fairly frequently polling a stop signal and then
            // exiting the thread.
            //
            // The [Thread.Abort()] exists on .NET CORE but it throws a 
            // [NotImplementedException].  This method does do something 
            // for .NET Framework, but most folks believe that using that
            // is a very bad idea anyway.
            //
            // So this means that you're going to have to poll [Terminator.TerminateNow]
            // frequently.  This is trivial in the example below, but for threads
            // performing complex long running activities, you may need to
            // sprinkle these checks across many of your methods.

            var shortDelay = TimeSpan.FromSeconds(1);

            while (!Terminator.TerminateNow)
            {
                Thread.Sleep(shortDelay);
            }
        }

        /// <summary>
        /// Demonstrates how a service task can be signalled to terminate.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TaskFunc()
        {
            while (true)
            {
                try
                {
                    // Note that we're sleeping here for 1 day!  This simulates
                    // service that's waiting (for a potentially long period of time)
                    // for something to do.

                    await Task.Delay(TimeSpan.FromDays(1), Terminator.CancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // This exception will be thrown when the terminator receives a
                    // signal to terminate the process because we passed the
                    // [Terminator.CancellationToken] to [Task.Async.Delay()].
                    // 
                    // The terminator calls [Cancel()] on it's cancellation token
                    // when the termination signal is received which causes any
                    // pending async operations that were passed the token to 
                    // abort and throw a [TaskCancelledException].
                    //
                    // This is a common .NET async programming pattern.
                    //
                    // We're going to use this exception as a signal to 
                    // exit the task.

                    return;
                }
            }
        }
    }
}
