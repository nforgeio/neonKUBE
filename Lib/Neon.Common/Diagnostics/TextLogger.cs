//-----------------------------------------------------------------------------
// FILE:	    TextLogger.cs
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

using Prometheus;

namespace Neon.Diagnostics
{
    /// <summary>
    /// A general purpose implementation of <see cref="INeonLogger"/> and <see cref="ILogger"/> that
    /// logs to STDERR by default, which is typical for container and Kubernetes applications.  The
    /// output can also be directed to a custom <see cref="TextWriter"/>.
    /// </summary>
    public class TextLogger : INeonLogger, ILogger
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly Counter LogEventCountByLevel = Metrics.CreateCounter(NeonHelper.NeonMetricsPrefix + "log_events_total", "Number of logged events.", "level");

        //---------------------------------------------------------------------
        // Instance members

        private ILogManager             logManager;
        private string                  module;
        private bool                    infoAsDebug;
        private string                  version;
        private TextWriter              writer;
        private string                  contextId;
        private Func<LogEvent, bool>    logFilter;
        private Func<bool>              isLogEnabledFunc;

        /// <inheritdoc/>
        public string ContextId => this.contextId;

        /// <inheritdoc/>
        public bool IsLogDebugEnabled => logManager.LogLevel >= LogLevel.Debug;

        /// <inheritdoc/>
        public bool IsLogTransientEnabled => logManager.LogLevel >= LogLevel.Transient;

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
        /// Constructor.
        /// </summary>
        /// <param name="logManager">The parent log manager or <c>null</c>.</param>
        /// <param name="module">Optionally identifies the event source module or <c>null</c>.</param>
        /// <param name="writer">Optionally specifies the output writer.  This defaults to <see cref="Console.Error"/>.</param>
        /// <param name="contextId">
        /// Optionally specifies additional information that can be used to identify context
        /// for logged events.  For example, the <c>Neon.Cadence</c> and <c>Neon.Temporal</c>
        /// clients use this to record the ID of the workflow recording events.
        /// </param>
        /// <param name="logFilter">
        /// Optionally specifies a filter predicate to be used for filtering log entries.  This examines
        /// the <see cref="LogEvent"/> and returns <c>true</c> if the event should be logged or <c>false</c>
        /// when it is to be ignored.  All events will be logged when this is <c>null</c>.
        /// </param>
        /// <param name="isLogEnabledFunc">
        /// Optionally specifies a function that will be called at runtime to
        /// determine whether to event logging is actually enabled.  This defaults
        /// to <c>null</c> which will always log events.
        /// </param>
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
        /// </note>
        /// </remarks>
        public TextLogger(
            ILogManager             logManager,
            string                  module           = null,
            TextWriter              writer           = null,
            string                  contextId        = null,
            Func<LogEvent, bool>    logFilter        = null,
            Func<bool>              isLogEnabledFunc = null)
        {
            this.logManager       = logManager ?? LogManager.Disabled;
            this.module           = module;
            this.version          = logManager.Version;
            this.writer           = writer ?? Console.Error;
            this.contextId        = contextId;
            this.logFilter        = logFilter;
            this.isLogEnabledFunc = isLogEnabledFunc;

            // $hack(jefflill):
            //
            // We're going to assume that ASP.NET related loggers are always
            // prefixed by: [Microsoft.AspNetCore]

            this.infoAsDebug = module != null && module.StartsWith("Microsoft.AspNetCore.");
        }

        /// <inheritdoc/>
        public bool IsLogLevelEnabled(LogLevel logLevel)
        {
            // Verify that logging isn't temporarily disabled.

            if (isLogEnabledFunc != null && !isLogEnabledFunc())
            {
                return false;
            }

            // Map into Neon log levels.

            switch (logLevel)
            {
                case LogLevel.None:

                    return false;

                case LogLevel.Critical:

                    return IsLogCriticalEnabled;

                case LogLevel.SError:

                    return IsLogSErrorEnabled;

                case LogLevel.Error:

                    return IsLogErrorEnabled;

                case LogLevel.Warn:

                    return IsLogWarnEnabled;

                case LogLevel.SInfo:

                    return IsLogSInfoEnabled;

                case LogLevel.Info:

                    return IsLogInfoEnabled;

                case LogLevel.Transient:

                    return IsLogTransientEnabled;

                case LogLevel.Debug:

                    return IsLogDebugEnabled;

                default:

                    throw new NotImplementedException();
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
        /// <param name="e">Optionally passed as a related exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        private void Log(LogLevel logLevel, string message, Exception e = null, string activityId = null)
        {
            // Increment the metrics counter for the event type.  Note that we're
            // going to increment the count even when logging for the level is
            // disabled.  This will help devops know when there might be issues
            // they may need to investigate by changing the log level.

            switch (logLevel)
            {
                case LogLevel.Critical:

                    LogEventCountByLevel.WithLabels("critical").Inc();
                    break;

                case LogLevel.Debug:

                    LogEventCountByLevel.WithLabels("debug").Inc();
                    break;

                case LogLevel.Transient:

                    LogEventCountByLevel.WithLabels("transient").Inc();
                    break;

                case LogLevel.Error:

                    LogEventCountByLevel.WithLabels("error").Inc();
                    break;

                case LogLevel.Info:

                    LogEventCountByLevel.WithLabels("info").Inc();
                    break;

                case LogLevel.None:

                    break;

                case LogLevel.SError:

                    LogEventCountByLevel.WithLabels("serror").Inc();
                    break;

                case LogLevel.SInfo:

                    LogEventCountByLevel.WithLabels("sinfo").Inc();
                    break;

                case LogLevel.Warn:

                    LogEventCountByLevel.WithLabels("warn").Inc();
                    break;

                default:

                    throw new NotImplementedException();
            }

            if (infoAsDebug && logLevel == LogLevel.Info)
            {
                if (!IsLogDebugEnabled)
                {
                    return;
                }

                logLevel = LogLevel.Debug;
            }

            if (logFilter != null)
            {
                var logEvent =
                    new LogEvent(
                        module:     this.module,
                        contextId:  contextId,
                        index:      0,                  // We don't set this when filtering
                        timeUtc:    DateTime.UtcNow,
                        logLevel:   logLevel,
                        message:    message,
                        activityId: activityId,
                        e:          e);

                if (!logFilter(logEvent))
                {
                    // Ignore filtered events.

                    return;
                }
            }

            string level;

            switch (logLevel)
            {
                case LogLevel.Critical:
                    
                    level = "CRITICAL";
                    break;

                case LogLevel.Debug:       
                    
                    level = "DEBUG";
                    break;

                case LogLevel.Transient:    
                    
                    level = "TRANSIENT";
                    break;

                case LogLevel.Error:      
                    
                    level = "ERROR";
                    break;

                case LogLevel.Info:       
                    
                    level = "INFO";
                    break;

                case LogLevel.None:    
                    
                    level = "NONE"; 
                    break;

                case LogLevel.SError:     
                    
                    level = "SERROR";
                    break;

                case LogLevel.SInfo:  
                    
                    level = "SINFO";
                    break;

                case LogLevel.Warn:     
                    
                    level = "WARN";
                    break;

                default:

                    throw new NotImplementedException();
            }

            message = Normalize(message);

            var version = string.Empty;

            if (!string.IsNullOrEmpty(this.version))
            {
                version = $" [version:{this.version}]";
            }

            var module = string.Empty;

            if (!string.IsNullOrEmpty(this.module))
            {
                module = $" [module:{this.module}]";
            }

            var activity = string.Empty;

            if (!string.IsNullOrEmpty(activityId))
            {
                activity = $" [activity-id:{activityId}]";
            }

            var context = string.Empty;

            if (!string.IsNullOrEmpty(contextId))
            {
                context = $" [context-id:{contextId}]";
            }

            var index = string.Empty;

            if (logManager.EmitIndex)
            {
                index = $" [index:{logManager.GetNextEventIndex()}]";
            }

            if (logManager.EmitTimestamp)
            {
                var timestamp = DateTime.UtcNow.ToString(NeonHelper.DateFormatTZOffset);

                writer.WriteLine($"[{timestamp}] [{level}]{version}{module}{activity}{context}{index} {message}");
            }
            else
            {
                writer.WriteLine($"[{level}]{version}{module}{activity}{context}{index} {message}");
            }
        }

        /// <inheritdoc/>
        public void LogDebug(string message, string activityId = null)
        {
            if (IsLogDebugEnabled)
            {
                try
                {
                    Log(LogLevel.Debug, message, activityId: activityId);
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogDebug(string message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (IsLogDebugEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Debug, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                    else
                    {
                        Log(LogLevel.Debug, $"{NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogTransient(string message, string activityId = null)
        {
            if (IsLogTransientEnabled)
            {
                try
                {
                    Log(LogLevel.Transient, message, activityId: activityId);
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogTransient(string message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (IsLogTransientEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Transient, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                    else
                    {
                        Log(LogLevel.Transient, $"{NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogError(string message, string activityId = null)
        {
            if (IsLogErrorEnabled)
            {
                try
                {
                    Log(LogLevel.Error, message, activityId: activityId);
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogError(string message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (IsLogErrorEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Error, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                    else
                    {
                        Log(LogLevel.Error, $"{NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogSError(string message, string activityId = null)
        {
            if (IsLogSErrorEnabled)
            {
                try
                {
                    Log(LogLevel.SError, message, activityId: activityId);
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogSError(string message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (IsLogSErrorEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.SError, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                    else
                    {
                        Log(LogLevel.SError, $"{NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogCritical(string message, string activityId = null)
        {
            if (IsLogCriticalEnabled)
            {
                try
                {
                    Log(LogLevel.Critical, message, activityId: activityId);
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogCritical(string message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (IsLogCriticalEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Critical, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                    else
                    {
                        Log(LogLevel.Critical, $"{NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogInfo(string message, string activityId = null)
        {
            if (IsLogInfoEnabled)
            {
                try
                {
                    Log(LogLevel.Info, message, activityId: activityId);
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogInfo(string message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (IsLogInfoEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Info, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                    else
                    {
                        Log(LogLevel.Info, $"{NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogSInfo(string message, string activityId = null)
        {
            if (IsLogSInfoEnabled)
            {
                try
                {
                    Log(LogLevel.SInfo, message, activityId: activityId);
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogSInfo(string message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (IsLogSInfoEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.SInfo, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                    else
                    {
                        Log(LogLevel.SInfo, $"{NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogWarn(string message, string activityId = null)
        {
            if (IsLogWarnEnabled)
            {
                try
                {
                    Log(LogLevel.Warn, message, activityId: activityId);
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void LogWarn(string message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (IsLogWarnEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log(LogLevel.Warn, $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
                    }
                    else
                    {
                        Log(LogLevel.Warn, $"{NeonHelper.ExceptionError(e, stackTrace: true)}", e: e, activityId: activityId);
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
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception e, Func<TState, Exception, string> formatter)
        {
            // It appears that formatters are not supposed to generate anything for
            // exceptions, so we don't have to do anything special.
            //
            //      https://github.com/aspnet/Logging/issues/442

            var message = formatter(state, null) ?? string.Empty;

            switch (ToNeonLevel(logLevel))
            {
                case LogLevel.Critical:

                    if (e == null)
                    {
                        LogCritical(message);
                    }
                    else
                    {
                        LogCritical(message, e);
                    }
                    break;

                case LogLevel.Debug:
                case LogLevel.Transient:

                    if (e == null)
                    {
                        LogDebug(message);
                    }
                    else
                    {
                        LogDebug(message, e);
                    }
                    break;

                case LogLevel.Error:

                    if (e == null)
                    {
                        LogError(message);
                    }
                    else
                    {
                        LogError(message, e);
                    }
                    break;

                case LogLevel.Info:

                    if (e == null)
                    {
                        LogInfo(message);
                    }
                    else
                    {
                        LogInfo(message, e);
                    }
                    break;

                case LogLevel.SError:

                    if (e == null)
                    {
                        LogSError(message);
                    }
                    else
                    {
                        LogSError(message, e);
                    }
                    break;

                case LogLevel.SInfo:

                    if (e == null)
                    {
                        LogSInfo(message);
                    }
                    else
                    {
                        LogSInfo(message, e);
                    }
                    break;

                case LogLevel.Warn:

                    if (e == null)
                    {
                        LogWarn(message);
                    }
                    else
                    {
                        LogWarn(message, e);
                    }
                    break;
            }
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
