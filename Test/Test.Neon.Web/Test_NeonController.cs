//-----------------------------------------------------------------------------
// FILE:	    Test_NeonController.cs
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
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Web;
using Neon.Xunit;

using Newtonsoft.Json.Linq;
using Test.Neon.Models;

using Xunit;
using Xunit.Abstractions;

namespace Test.Neon.Web.Controller
{
    //-------------------------------------------------------------------------
    // Test controller based on [NeonController]

    public class TestController : NeonController
    {
        [HttpGet("/NeonController/LogCritical")]
        public void LogCritical()
        {
            LogCritical("critical");
        }

        [HttpGet("/NeonController/LogSError")]
        public void LogSError()
        {
            LogSError("serror");
        }

        [HttpGet("/NeonController/LogError")]
        public void LogError()
        {
            LogError("error");
        }

        [HttpGet("/NeonController/LogWarn")]
        public void LogWarn()
        {
            LogWarn("warn");
        }

        [HttpGet("/NeonController/LogSInfo")]
        public void LogSInfo()
        {
            LogSInfo("sinfo");
        }

        [HttpGet("/NeonController/LogInfo")]
        public void LogInfo()
        {
            LogInfo("info");
        }

        [Route("/NeonController/LogTransient")]
        public void LogTransient()
        {
            LogTransient("transient");
        }

        [HttpGet("/NeonController/LogDebug")]
        public void LogDebug()
        {
            LogDebug("debug");
        }

        [HttpGet("/NeonController/ThrowException")]
        public void ThrowException()
        {
            // Throw an unhandled exception.

            throw new TimeoutException("test timeout");
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddNeon();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseEndpoints(routes =>
            {
                routes.MapControllers();
            });
        }
    }

    /// <summary>
    /// Basic tests for <see cref="NeonControllerBase"/>.
    /// </summary>
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_TestControllerBase : IClassFixture<AspNetFixture>
    {
        private AspNetFixture       fixture;
        private TestOutputWriter    testWriter;
        private HttpClient          httpClient;

        public Test_TestControllerBase(AspNetFixture fixture, ITestOutputHelper outputHelper)
        {
            var testPort = 0;
            var logLevel = global::Neon.Diagnostics.LogLevel.None;

            this.fixture    = fixture;
            this.testWriter = new TestOutputWriter(outputHelper);

            fixture.Start<Startup>(port: testPort, logWriter: testWriter, logLevel: logLevel);

            this.fixture    = fixture;
            this.httpClient = fixture.HttpClient;
            this.testWriter = new TestOutputWriter(outputHelper);
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonWeb)]
        public async Task LogLevels()
        {
            var logManager = LogManager.Default;

            Assert.NotNull(logManager);

            try
            {
                // Configure the log manager to use [TestLogger] loggers.

                logManager.LoggerCreator =
                    (LogManager manager, string module, TextWriter writer, string contextId, Func<bool> isLogEnabledFunc) =>
                    {
                        return new TestLogger(manager, module, writer, contextId, isLogEnabledFunc);
                    };

                logManager.EmitIndex = true;

                TestLogger.ClearEvents();
                Assert.Empty(TestLogger.GetEvents());

                // Verify that the controller logging methods work.

                //-----------------------------------------
                // LogLevel.None

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.None;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                var events = TestLogger.GetEvents();

                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // LogLevel.Critical

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.Critical;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // LogLevel.SError

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.SError;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // LogLevel.Error

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.Error;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // LogLevel.Warn

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.Warn;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // LogLevel.SInfo

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.SInfo;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // LogLevel.Info

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.Info;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // LogLevel.Transient

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.Transient;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.Empty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // LogLevel.Debug

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.Debug;

                await httpClient.GetSafeAsync("/NeonController/LogCritical");
                await httpClient.GetSafeAsync("/NeonController/LogSError");
                await httpClient.GetSafeAsync("/NeonController/LogError");
                await httpClient.GetSafeAsync("/NeonController/LogWarn");
                await httpClient.GetSafeAsync("/NeonController/LogSInfo");
                await httpClient.GetSafeAsync("/NeonController/LogInfo");
                await httpClient.GetSafeAsync("/NeonController/LogTransient");
                await httpClient.GetSafeAsync("/NeonController/LogDebug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Debug));

                //-----------------------------------------
                // Verify that an unhandled exception is logged as an error.  Note
                // that this request is going to throw an internal server exception
                // because we're throwing an unhandled exception, so we'll use the
                // non-safe GET method.

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.Debug;

                await httpClient.GetAsync("/NeonController/ThrowException");

                events = TestLogger.GetEvents();

                Assert.Single(events);

                var evt = events[0];

                Assert.Equal(LogLevel.Error, evt.LogLevel);
                Assert.StartsWith("System.TimeoutException: test timeout", evt.Message);
            }
            finally
            {
                // Reset the log manager so we don't impact other test cases.

                logManager.Reset();
            }
        }
    }
}
