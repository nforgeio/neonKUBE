//-----------------------------------------------------------------------------
// FILE:	    NeonLogger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Common;

namespace Neon.Diagnostics
{
    /// <summary>
    /// A general purpose implementation of <see cref="INeonLogger"/> and <see cref="ILogger"/>.
    /// </summary>
    internal class NeonLogger : INeonLogger, ILogger
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Formats metric fields into a message.
        /// </summary>
        /// <param name="txtFields">The text fields (or <c>null</c>).</param>
        /// <param name="numFields">The numeric fields (or <c>null</c>).</param>
        /// <exception cref="ArgumentException">
        /// Thrown if either of <paramref name="txtFields"/> or <paramref name="numFields"/> 
        /// includes more than 10 items.
        /// </exception>
        /// <returns>The formatted message.</returns>
        internal static string FormatMetrics(IEnumerable<string> txtFields, IEnumerable<double> numFields)
        {
            var sb    = new StringBuilder();
            var index = 0;

            if (txtFields != null)
            {
                if (txtFields.Count() > 10)
                {
                    throw new ArgumentException($"[{nameof(txtFields)}] count exceeds 10.");
                }

                foreach (var field in txtFields)
                {
                    sb.AppendWithSeparator($"[txt.{index}={field}]");
                }
            }

            if (numFields != null)
            {
                if (numFields.Count() > 10)
                {
                    throw new ArgumentException($"[{nameof(numFields)}] count exceeds 10.");
                }

                foreach (var field in numFields)
                {
                    sb.AppendWithSeparator($"[num.{index}={field}]");
                }
            }

            return sb.ToString();
        }

        //---------------------------------------------------------------------
        // Instance members

        private ILogManager logManager;
        private string      sourceModule;
        private bool        infoAsDebug;
        private long        emitCount;
        private TextWriter  writer;

        /// <inheritdoc/>
        public bool IsLogDebugEnabled => logManager.LogLevel >= LogLevel.Debug;

        /// <inheritdoc/>
        public bool IsLogErrorEnabled => logManager.LogLevel >= LogLevel.Error;

        /// <inheritdoc/>
        public bool IsLogSErrorEnabled => logManager.LogLevel >= LogLevel.SError;

        /// <inheritdoc/>
        public bool IsLogCriticalEnabled => logManager.LogLevel >= LogLevel.Critical;

        /// <inheritdoc/>
        public bool IsLogInfoEnabled => logManager.LogLevel >= LogLevel.Info;

        /// <inheritdoc/>
        public bool IsLogSInfoEnabled => logManager.LogLevel >= LogLevel.SInfo;

        /// <inheritdoc/>
        public bool IsLogWarnEnabled => logManager.LogLevel >= LogLevel.Warn;

        /// <summary>
        /// Constructs a named instance.
        /// </summary>
        /// <param name="logManager">The parent log manager or <c>null</c>.</param>
        /// <param name="sourceModule">Optionally identifies the event source module or <c>null</c>.</param>
        /// <param name="noisyAspNet">Optionally enables normal (noisy) logging of ASP.NET <b>INFO</b> events (see note in remarks).</param>
        /// <param name="writer">Optionally specifies the output writer.  This defaults to <see cref="Console.Error"/>.</param>
        /// <remarks>
        /// <para>
        /// The instances returned will log nothing if <paramref name="logManager"/>
        /// is passed as <c>null</c>.
        /// </para>
        /// <note>
        /// <para>
        /// ASP.NET is super noisy, logging three or four <b>INFO</b> events per request.  There
        /// doesn't appear to an easy way to change this behavior, I'd really like to recategorize
        /// these as <b>DEBUG</b> to reduce pressure on the logs.
        /// </para>
        /// <para>
        /// We accomplish this by default when <paramref name="noisyAspNet"/> is passed as
        /// <c>false</c>.  This is used to signal that the instance should perform special 
        /// ASP.NET level filtering.
        /// </para>
        /// </note>
        /// </remarks>
        public NeonLogger(ILogManager logManager, string sourceModule = null, bool noisyAspNet = false, TextWriter writer = null)
        {
            this.logManager   = logManager ?? LogManager.Disabled;
            this.sourceModule = sourceModule ?? string.Empty;
            this.writer       = writer ?? Console.Error;

            // $hack(jeff.lill):
            //
            // We're going to assume that ASP.NET related loggers are always
            // prefixed by: [Microsoft.AspNetCore]

            this.infoAsDebug = !noisyAspNet && sourceModule != null && sourceModule.StartsWith("Microsoft.AspNetCore.");
        }

        /// <inheritdoc/>
        public bool IsLogLevelEnabled(LogLevel logLevel)
        {
            // Map into Neon log levels.

            switch (logLevel)
            {
                case LogLevel.Debug:

                    return IsLogDebugEnabled;

                case LogLevel.Info:

                    return IsLogInfoEnabled;

                case LogLevel.Warn:

                    return IsLogWarnEnabled;

                case LogLevel.Error:

                    return IsLogErrorEnabled;

                case LogLevel.Critical:

                    return IsLogCriticalEnabled;

                case LogLevel.None:
                default:

                    return false;
            }
        }

        /// <summary>
        /// Normalizes a log message by escaping any backslashes and replacing any line
        /// endings with "\n".  This converts multi-line message to a single line.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The normalized message.</returns>
        private static string Normalize(string message)
        {
            if (message == null)
            {
                return string.Empty;
            }

            message = message.Replace("\\", "\\\\");
            message = message.Replace("\r\n", "\\n");
            message = message.Replace("\n", "\\n");

            return message;
        }

        /// <summary>
        /// Logs an event.
        /// </summary>
        /// <param name="logLevel">The event level.</param>
        /// <param name="message">The event message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        private void Log(LogLevel logLevel, string message, string activityId = null)
        {
            if (infoAsDebug && logLevel == LogLevel.Info)
            {
                if (!IsLogDebugEnabled)
                {
                    return;
                }

                logLevel = LogLevel.Debug;
            }

            var level = string.Empty;

            switch (logLevel)
            {
                case LogLevel.Critical: level = "CRITICAL"; break;
                case LogLevel.Debug:    level = "DEBUG"; break;
                case LogLevel.Error:    level = "ERROR"; break;
                case LogLevel.Info:     level = "INFO"; break;
                case LogLevel.None:     level = "NONE"; break;
                case LogLevel.SError:   level = "SERROR"; break;
                case LogLevel.SInfo:    level = "SINFO"; break;
                case LogLevel.Warn:     level = "WARN"; break;
            }

            message = Normalize(message);

            lock (sourceModule)
            {
                var module = string.Empty;

                if (!string.IsNullOrEmpty(this.sourceModule))
                {
                    module = $" [module:{this.sourceModule}]";
                }

                var activity = string.Empty;

                if (!string.IsNullOrEmpty(activityId))
                {
                    activityId = $" [activity-id:{activityId}]";
                }

                var index = string.Empty;

                if (logManager.EmitIndex)
                {
                    index = $" [index:{Interlocked.Increment(ref emitCount)}]";
                }

                if (logManager.EmitTimestamp)
                {
                    var timestamp = DateTime.UtcNow.ToString(NeonHelper.DateFormatTZOffset);

                    writer.WriteLine($"[{timestamp}] [{level}]{activity}{module}{index} {message}");
                }
                else
                {
                    writer.WriteLine($"[{level}]{activity}{module}{index} {message}");
                }
            }
        }

        /// <inheritdoc/>
        public void LogDebug(object message, string activityId = null)
        {
            if (IsLogDebugEnabled)
            {
                try
                {
                    Log(LogLevel.Debug, message?.ToString());
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogDebug(object message, Exception e, string activityId = null)
        {
            if (IsLogDebugEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Debug, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log(LogLevel.Debug, $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogError(object message, string activityId = null)
        {
            if (IsLogErrorEnabled)
            {
                try
                {
                    Log(LogLevel.Error, message?.ToString());
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogError(object message, Exception e, string activityId = null)
        {
            if (IsLogErrorEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Error, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log(LogLevel.Error, $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogSError(object message, string activityId = null)
        {
            if (IsLogErrorEnabled)
            {
                try
                {
                    Log(LogLevel.SError, message?.ToString());
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogSError(object message, Exception e, string activityId = null)
        {
            if (IsLogSErrorEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.SError, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log(LogLevel.SError, $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogCritical(object message, string activityId = null)
        {
            if (IsLogCriticalEnabled)
            {
                try
                {
                    Log(LogLevel.Critical, message?.ToString());
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogCritical(object message, Exception e, string activityId = null)
        {
            if (IsLogCriticalEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Critical, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log(LogLevel.Critical, $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogInfo(object message, string activityId = null)
        {
            if (IsLogInfoEnabled)
            {
                try
                {
                    Log(LogLevel.Info, message?.ToString());
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogInfo(object message, Exception e, string activityId = null)
        {
            if (IsLogInfoEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Info, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log(LogLevel.Info, $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, string activityId = null)
        {
            if (IsLogInfoEnabled)
            {
                try
                {
                    Log(LogLevel.SInfo, message?.ToString());
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, Exception e, string activityId = null)
        {
            if (IsLogInfoEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.SInfo, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log(LogLevel.SInfo, $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogWarn(object message, string activityId = null)
        {
            if (IsLogWarnEnabled)
            {
                try
                {
                    Log(LogLevel.Warn, message?.ToString());
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogWarn(object message, Exception e, string activityId = null)
        {
            if (IsLogWarnEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Warn, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log(LogLevel.Warn, $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        //---------------------------------------------------------------------
        // ILogger implementation
        //
        // We're implementing this so that Neon logging will be compatible with 
        // non-Neon components.

        /// <summary>
        /// Do-nothing disposable returned by <see cref="BeginScope{TState}(TState)"/>.
        /// </summary>
        public sealed class Scope : IDisposable
        {
            /// <summary>
            /// Internal connstructor.
            /// </summary>
            internal Scope()
            {
            }

            /// <inheritdoc/>
            public void Dispose()
            {
            }
        }

        private static Scope scopeGlobal = new Scope(); // This can be static because it doesn't actually do anything.

        /// <summary>
        /// Converts a Microsoft log level into the corresponding Neon level.
        /// </summary>
        /// <param name="logLevel">The Microsoft log level.</param>
        /// <returns>The Neon <see cref="LogLevel"/>.</returns>
        private static LogLevel ToNeonLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            switch (logLevel)
            {
                default:
                case Microsoft.Extensions.Logging.LogLevel.None:

                    return LogLevel.None;

                case Microsoft.Extensions.Logging.LogLevel.Debug:
                case Microsoft.Extensions.Logging.LogLevel.Trace:

                    return LogLevel.Debug;

                case Microsoft.Extensions.Logging.LogLevel.Information:

                    return LogLevel.Info;

                case Microsoft.Extensions.Logging.LogLevel.Warning:

                    return LogLevel.Warn;

                case Microsoft.Extensions.Logging.LogLevel.Error:

                    return LogLevel.Error;

                case Microsoft.Extensions.Logging.LogLevel.Critical:

                    return LogLevel.Critical;
            }
        }

        /// <inheritdoc/>
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            // It appears that formatters are not supposed to generate anything for
            // exceptions, so we don't have to do anything special.
            //
            //      https://github.com/aspnet/Logging/issues/442

            var message = formatter(state, null) ?? string.Empty;

            switch (ToNeonLevel(logLevel))
            {
                case LogLevel.Critical:

                    if (exception == null)
                    {
                        LogCritical(message);
                    }
                    else
                    {
                        LogCritical(message, exception);
                    }
                    break;

                case LogLevel.Debug:

                    if (exception == null)
                    {
                        LogDebug(message);
                    }
                    else
                    {
                        LogDebug(message, exception);
                    }
                    break;

                case LogLevel.Error:

                    if (exception == null)
                    {
                        LogError(message);
                    }
                    else
                    {
                        LogError(message, exception);
                    }
                    break;

                case LogLevel.Info:

                    if (exception == null)
                    {
                        LogInfo(message);
                    }
                    else
                    {
                        LogInfo(message, exception);
                    }
                    break;

                case LogLevel.SError:

                    if (exception == null)
                    {
                        LogSError(message);
                    }
                    else
                    {
                        LogSError(message, exception);
                    }
                    break;

                case LogLevel.SInfo:

                    if (exception == null)
                    {
                        LogSInfo(message);
                    }
                    else
                    {
                        LogSInfo(message, exception);
                    }
                    break;

                case LogLevel.Warn:

                    if (exception == null)
                    {
                        LogWarn(message);
                    }
                    else
                    {
                        LogWarn(message, exception);
                    }
                    break;
            }
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, IEnumerable<string> txtFields, IEnumerable<double> numFields)
        {
            LogMetrics(level, txtFields, numFields);
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params string[] txtFields)
        {
            LogMetrics(level, txtFields);
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params double[] numFields)
        {
            LogMetrics(level, numFields);
        }

        /// <inheritdoc/>
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return IsLogLevelEnabled(ToNeonLevel(logLevel));
        }

        /// <inheritdoc/>
        public IDisposable BeginScope<TState>(TState state)
        {
            // We're not doing anything special for this right now.

            return scopeGlobal;
        }
    }
}
