//-----------------------------------------------------------------------------
// FILE:	    AspNetFixture.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Net;

using Xunit.Abstractions;

using LogLevel = Neon.Diagnostics.LogLevel;

namespace Neon.Xunit
{
    /// <summary>
    /// Fixture for testing ASP.NET Core based websites and services.
    /// </summary>
    public class AspNetFixture : TestFixture
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Handles site logging if enabled below.
        /// </summary>
        private class LoggingProvider : ILoggerProvider
        {
            private LogManager  logManager;

            public LoggingProvider(TextWriter logWriter, LogLevel logLevel)
            {
                this.logManager = new LogManager(writer: logWriter)
                {
                    LogLevel = logLevel
                };
            }

            public ILogger CreateLogger(string categoryName)
            {
                return (ILogger)logManager.GetLogger(categoryName);
            }

            public void Dispose()
            {
                if (logManager != null)
                {
                    logManager.Dispose();
                    logManager = null;
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private Action<IWebHostBuilder> hostConfigurator;
        private TextWriter              logWriter = null;
        private LogLevel                logLevel  = LogLevel.None;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public AspNetFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~AspNetFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns a <see cref="JsonClient"/> suitable for querying the service.
        /// </summary>
        public JsonClient JsonClient { get; private set; }

        /// <summary>
        /// Returns an <see cref="HttpClient"/> suitable for querying the service.
        /// </summary>
        public HttpClient HttpClient => JsonClient?.HttpClient;

        /// <summary>
        /// Returns the base URI for the running service.
        /// </summary>
        public Uri BaseAddress => JsonClient?.BaseAddress;

        /// <summary>
        /// Returns the service's <see cref="IWebHost"/>.
        /// </summary>
        public IWebHost WebHost { get; private set; }

        /// <summary>
        /// <para>
        /// Starts the ASP.NET service using the default controller factory.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed{TStartup}(Action{IWebHostBuilder}, int, TestOutputWriter, LogLevel)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <typeparam name="TStartup">The startup class for the service.</typeparam>
        /// <param name="hostConfigurator">Optional action providing for customization of the hosting environment.</param>
        /// <param name="port">The port where the server will listen or zero to allow the operating system to select a free port.</param>
        /// <param name="logWriter">Optionally specifies a test output writer.</param>
        /// <param name="logLevel">Optionally specifies the log level.  This defaults to <see cref="LogLevel.None"/>.</param>
        /// <remarks>
        /// <para>
        /// You can capture ASP.NET and service logs into your unit test logs by passing <paramref name="logWriter"/> as 
        /// non-null and <paramref name="logLevel"/> as something other than <see cref="LogLevel.None"/>.  You'll need
        /// to obtain a <see cref="ITestOutputHelper"/> instance from Xunit via dependency injection by adding a parameter
        /// to your test constructor and then creating a <see cref="TestOutputWriter"/> from it, like:
        /// </para>
        /// <code language="c#">
        /// public class MyTest : IClassFixture&lt;AspNetFixture&gt;
        /// {
        ///     private AspNetFixture               fixture;
        ///     private TestAspNetFixtureClient     client;
        ///     private TestOutputWriter            testWriter;
        ///
        ///     public Test_EndToEnd(AspNetFixture fixture, ITestOutputHelper outputHelper)
        ///     {
        ///         this.fixture    = fixture;
        ///         this.testWriter = new TestOutputWriter(outputHelper);
        ///
        ///         fixture.Start&lt;Startup&gt;(logWriter: testWriter, logLevel: Neon.Diagnostics.LogLevel.Debug);
        ///
        ///         client = new TestAspNetFixtureClient()
        ///         {
        ///             BaseAddress = fixture.BaseAddress
        ///         };
        ///      }
        /// }
        /// </code>
        /// </remarks>
        public void Start<TStartup>(Action<IWebHostBuilder> hostConfigurator = null, int port = 0, TestOutputWriter logWriter = null, LogLevel logLevel = LogLevel.None)
            where TStartup : class
        {
            base.CheckDisposed();

            base.Start(
                () =>
                {
                    StartAsComposed<TStartup>(hostConfigurator, port, logWriter, logLevel);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <typeparam name="TStartup">The startup class for the service.</typeparam>
        /// <param name="hostConfigurator">Optional action providing for customization of the hosting environment.</param>
        /// <param name="port">The port where the server will listen or zero to allow the operating system to select a free port.</param>
        /// <param name="logWriter">Optionally specifies a test output writer.</param>
        /// <param name="logLevel">Optionally specifies the log level.  This defaults to <see cref="LogLevel.None"/>.</param>
        /// <remarks>
        /// <para>
        /// You can capture ASP.NET and service logs into your unit test logs by passing <paramref name="logWriter"/> as 
        /// non-null and <paramref name="logLevel"/> as something other than <see cref="LogLevel.None"/>.  You'll need
        /// to obtain a <see cref="ITestOutputHelper"/> instance from Xunit via dependency injection by adding a parameter
        /// to your test constructor and then creating a <see cref="TestOutputWriter"/> from it, like:
        /// </para>
        /// <code language="c#">
        /// public class MyTest : IClassFixture&lt;AspNetFixture&gt;
        /// {
        ///     private AspNetFixture               fixture;
        ///     private TestAspNetFixtureClient     client;
        ///     private TestOutputWriter            testWriter;
        ///
        ///     public Test_EndToEnd(AspNetFixture fixture, ITestOutputHelper outputHelper)
        ///     {
        ///         this.fixture    = fixture;
        ///         this.testWriter = new TestOutputWriter(outputHelper);
        ///
        ///         fixture.Start&lt;Startup&gt;(logWriter: testWriter, logLevel: Neon.Diagnostics.LogLevel.Debug);
        ///
        ///         client = new TestAspNetFixtureClient()
        ///         {
        ///             BaseAddress = fixture.BaseAddress
        ///         };
        ///      }
        /// }
        /// </code>
        /// </remarks>
        public void StartAsComposed<TStartup>(Action<IWebHostBuilder> hostConfigurator = null, int port = 0, TestOutputWriter logWriter = null, LogLevel logLevel = LogLevel.None)
            where TStartup : class
        {
            this.hostConfigurator = hostConfigurator;
            this.logWriter        = logWriter;
            this.logLevel         = logLevel;

            base.CheckWithinAction();

            if (IsRunning)
            {
                return;
            }

            StartServer<TStartup>(port);

            // Get the address where the server is listening and create the client.

            JsonClient = new JsonClient()
            {
                BaseAddress = new Uri(WebHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.OfType<string>().FirstOrDefault())
            };

            IsRunning = true;
        }

        /// <summary>
        /// Starts the service using the default controller factory.
        /// </summary>
        /// <param name="port">The port where the server will listen.</param>
        private void StartServer<TStartup>(int port)
            where TStartup : class
        {
            Covenant.Requires<ArgumentException>(port == 0 || NetHelper.IsValidPort(port), nameof(port));

            var app = new WebHostBuilder()
                .UseStartup<TStartup>()
                .UseKestrel(
                    options =>
                    {
                        options.Listen(IPAddress.Loopback, port);
                    });

            if (logWriter != null && logLevel != LogLevel.None)
            {
                app.ConfigureLogging(
                    (hostingContext, logging) =>
                    {
                        logging.AddProvider(new LoggingProvider(logWriter, logLevel));
                    });
            }

            hostConfigurator?.Invoke(app);
            WebHost = app.Build();
            WebHost.Start();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    Reset();
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Restarts the web service.
        /// </summary>
        /// <typeparam name="TStartup">Specifies the web service startup class.</typeparam>
        public void Restart<TStartup>()
            where TStartup : class
        {
            Covenant.Requires<InvalidOperationException>(IsRunning);

            WebHost.StopAsync().Wait();
            StartServer<TStartup>(BaseAddress.Port);
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            if (!IsDisposed)
            {
                JsonClient.Dispose();
                WebHost.StopAsync().Wait();

                JsonClient = null;
                WebHost    = null;
            }
        }
    }
}
