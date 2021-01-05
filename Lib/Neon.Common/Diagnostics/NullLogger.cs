//-----------------------------------------------------------------------------
// FILE:	    NullLogger.cs
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Common;

namespace Neon.Diagnostics
{
    /// <summary>
    /// Implements an <see cref="INeonLogger"/> that simply drops all logged events.
    /// </summary>
    public class NullLogger : INeonLogger, ILogger
    {
        private ILogManager     logManager;
        private string          sourceModule;
        private TextWriter      writer;
        private string          contextId;

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
        /// <param name="sourceModule">Optionally identifies the event source module or <c>null</c>.</param>
        /// <param name="contextId">
        /// Optionally specifies additional information that can be used to identify
        /// context for logged events.  For example, the Neon.Cadence client uses this 
        ///  to record the ID of the workflow recording events.
        /// </param>
        /// <remarks>
        /// <para>
        /// The instances returned will log nothing if <paramref name="logManager"/>
        /// is passed as <c>null</c>.
        /// </para>
        /// </remarks>
        public NullLogger(
            ILogManager     logManager, 
            string          sourceModule     = null, 
            string          contextId        = null)
        {
            this.logManager   = logManager ?? LogManager.Disabled;
            this.sourceModule = sourceModule ?? string.Empty;
            this.writer       = writer ?? Console.Error;
            this.contextId    = contextId;
        }

        /// <inheritdoc/>
        public bool IsLogLevelEnabled(LogLevel logLevel)
        {
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
        /// Logs an event.
        /// </summary>
        /// <param name="logLevel">The event level.</param>
        /// <param name="message">The event message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        private void Log(LogLevel logLevel, string message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogDebug(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogDebug(object message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));
        }

        /// <inheritdoc/>
        public void LogTransient(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogTransient(object message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));
        }

        /// <inheritdoc/>
        public void LogError(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogError(object message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));
        }

        /// <inheritdoc/>
        public void LogSError(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSError(object message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));
        }

        /// <inheritdoc/>
        public void LogCritical(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogCritical(object message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));
        }

        /// <inheritdoc/>
        public void LogInfo(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogInfo(object message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));
        }

        /// <inheritdoc/>
        public void LogWarn(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogWarn(object message, Exception e, string activityId = null)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));
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
