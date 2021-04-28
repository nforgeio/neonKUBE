//-----------------------------------------------------------------------------
// FILE:	    Test_Logger.TextLogger.cs
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
        public void Verify_TextLogger()
        {
            // Verify that we can configure and use the [TestLogger].

            var logManager = LogManager.Default;

            Assert.NotNull(logManager);

            try
            {
                // Configure the log manager to use [TextLogger] loggers while redirecting
                // the output to a local [StringWriter].

                var logBuilder = new StringBuilder();
                var logWriter  = new StringWriter(logBuilder);

                logManager.LoggerCreator =
                    (LogManager manager, string module, TextWriter writer, string contextId, Func<bool> isLogEnabledFunc) =>
                    {
                        return new TextLogger(manager, module, logWriter, contextId, isLogEnabledFunc);
                    };

                logManager.EmitIndex = true;

                logBuilder.Clear();
                Assert.Empty(logBuilder.ToString());

                var log = logManager.GetLogger();

                // Log some events and verify.

                log.LogInfo("information");
                log.LogError("error");

                var lines = SplitLines(logBuilder);

                Assert.Equal(2, lines.Length);

                Assert.Contains("[INFO]", lines[0]);
                Assert.Contains("[index:1]", lines[0]);
                Assert.Contains("information", lines[0]);

                Assert.Contains("[ERROR]", lines[1]);
                Assert.Contains("[index:2]", lines[1]);
                Assert.Contains("error", lines[1]);
            }
            finally
            {
                // Reset the log manager so we don't impact other test cases.

                logManager.Reset();
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Verify_TextLogger_Levels()
        {
            // Verify that log level filtering works correctly.

            var logManager = LogManager.Default;

            Assert.NotNull(logManager);

            try
            {
                // Configure the log manager to use [TextLogger] loggers while redirecting
                // the output to a local [StringWriter].

                var logBuilder = new StringBuilder();
                var logWriter = new StringWriter(logBuilder);

                logManager.LoggerCreator =
                    (LogManager manager, string module, TextWriter writer, string contextId, Func<bool> isLogEnabledFunc) =>
                    {
                        return new TextLogger(manager, module, logWriter, contextId, isLogEnabledFunc);
                    };

                logManager.EmitIndex = true;

                logBuilder.Clear();
                Assert.Empty(logBuilder.ToString());

                var log = logManager.GetLogger();

                //-----------------------------------------
                // LogLevel.None

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.None;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                var lines = SplitLines(logBuilder);

                Assert.Empty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.Empty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.Empty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.Empty(lines.Where(line => line.Contains("[WARN]")));
                Assert.Empty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[INFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.Empty(lines.Where(line => line.Contains("[DEBUG]")));

                //-----------------------------------------
                // LogLevel.Critical

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.Critical;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                lines = SplitLines(logBuilder);

                Assert.NotEmpty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.Empty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.Empty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.Empty(lines.Where(line => line.Contains("[WARN]")));
                Assert.Empty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[INFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.Empty(lines.Where(line => line.Contains("[DEBUG]")));

                //-----------------------------------------
                // LogLevel.SError

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.SError;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                lines = SplitLines(logBuilder);

                Assert.NotEmpty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.Empty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.Empty(lines.Where(line => line.Contains("[WARN]")));
                Assert.Empty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[INFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.Empty(lines.Where(line => line.Contains("[DEBUG]")));

                //-----------------------------------------
                // LogLevel.Error

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.Error;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                lines = SplitLines(logBuilder);

                Assert.NotEmpty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.Empty(lines.Where(line => line.Contains("[WARN]")));
                Assert.Empty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[INFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.Empty(lines.Where(line => line.Contains("[DEBUG]")));

                //-----------------------------------------
                // LogLevel.Warn

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.Warn;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                lines = SplitLines(logBuilder);

                Assert.NotEmpty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[WARN]")));
                Assert.Empty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[INFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.Empty(lines.Where(line => line.Contains("[DEBUG]")));

                //-----------------------------------------
                // LogLevel.SInfo

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.SInfo;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                lines = SplitLines(logBuilder);

                Assert.NotEmpty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[WARN]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[INFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.Empty(lines.Where(line => line.Contains("[DEBUG]")));

                //-----------------------------------------
                // LogLevel.Info

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.Info;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                lines = SplitLines(logBuilder);

                Assert.NotEmpty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[WARN]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[INFO]")));
                Assert.Empty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.Empty(lines.Where(line => line.Contains("[DEBUG]")));

                //-----------------------------------------
                // LogLevel.Transient

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.Transient;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                lines = SplitLines(logBuilder);

                Assert.NotEmpty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[WARN]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[INFO]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.Empty(lines.Where(line => line.Contains("[DEBUG]")));

                //-----------------------------------------
                // LogLevel.Debug

                logBuilder.Clear();

                logManager.LogLevel = LogLevel.Debug;

                log.LogCritical("critical");
                log.LogSError("serror");
                log.LogError("error");
                log.LogWarn("warn");
                log.LogSInfo("sinfo");
                log.LogInfo("info");
                log.LogTransient("transient");
                log.LogDebug("debug");

                lines = SplitLines(logBuilder);

                Assert.NotEmpty(lines.Where(line => line.Contains("[CRITICAL]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[ERROR]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[WARN]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[SINFO]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[INFO]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[TRANSIENT]")));
                Assert.NotEmpty(lines.Where(line => line.Contains("[DEBUG]")));
            }
            finally
            {
                // Reset the log manager so we don't impact other test cases.

                logManager.Reset();
            }
        }

        /// <summary>
        /// Splits a string builder into an array of lines.
        /// </summary>
        /// <param name="sb">The source builder.</param>
        /// <returns>The line array.</returns>
        private string[] SplitLines(StringBuilder sb)
        {
            return sb.ToString().Replace("\r\n", "\n").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
