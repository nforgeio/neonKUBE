//-----------------------------------------------------------------------------
// FILE:	    Test_Logger.TestLogger.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_Logger
    {
        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Verify_TestLogger()
        {
            // Verify that we can configure and use the [TestLogger].

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

                var log = logManager.GetLogger();

                // Log some events and verify.

                var startTimeUtc = DateTime.UtcNow;

                log.LogInfo("information");
                log.LogError("error");

                var endTimeUtc = DateTime.UtcNow;
                var events     = TestLogger.GetEvents();

                Assert.Equal(2, events.Length);

                var evt = events[0];

                Assert.True(startTimeUtc <= evt.TimeUtc && evt.TimeUtc <= endTimeUtc);
                Assert.Null(evt.ActivityId);
                Assert.Null(evt.ContextId);
                Assert.Null(evt.Exception);
                Assert.Equal(1, evt.Index);
                Assert.Equal(LogLevel.Info, evt.LogLevel);
                Assert.Null(evt.Module);
                Assert.Equal("information", evt.Message);

                evt = events[1];

                Assert.True(startTimeUtc <= evt.TimeUtc && evt.TimeUtc <= endTimeUtc);
                Assert.Null(evt.ActivityId);
                Assert.Null(evt.ContextId);
                Assert.Null(evt.Exception);
                Assert.Equal(2, evt.Index);
                Assert.Equal(LogLevel.Error, evt.LogLevel);
                Assert.Null(evt.Module);
                Assert.Equal("error", evt.Message);
            }
            finally
            {
                // Reset the log manager so we don't impact other test cases.

                logManager.Reset();
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Verify_TestLogger_Levels()
        {
            // Verify that log level filtering works correctly.

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

                var log = logManager.GetLogger();

                //-----------------------------------------
                // LogLevel.None

                TestLogger.ClearEvents();

                logManager.LogLevel = LogLevel.None;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

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

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

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

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

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

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

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

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

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

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

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

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

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

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

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

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                events = TestLogger.GetEvents();

                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Critical));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SError));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Error));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Warn));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.SInfo));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Info));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Transient));
                Assert.NotEmpty(events.Where(evt => evt.LogLevel == LogLevel.Debug));
            }
            finally
            {
                // Reset the log manager so we don't impact other test cases.

                logManager.Reset();
            }
        }
    }
}
