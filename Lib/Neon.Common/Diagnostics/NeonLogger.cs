//-----------------------------------------------------------------------------
// FILE:	    NeonLogger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
        private ILogManager logManager;
        private string      name;
        private long        emitCount;

        /// <inheritdoc/>
        public bool IsDebugEnabled => logManager.LogLevel >= LogLevel.Debug;

        /// <inheritdoc/>
        public bool IsErrorEnabled => logManager.LogLevel >= LogLevel.Error;

        /// <inheritdoc/>
        public bool IsSErrorEnabled => logManager.LogLevel >= LogLevel.SError;

        /// <inheritdoc/>
        public bool IsCriticalEnabled => logManager.LogLevel >= LogLevel.Critical;

        /// <inheritdoc/>
        public bool IsInfoEnabled => logManager.LogLevel >= LogLevel.Info;

        /// <inheritdoc/>
        public bool IsSInfoEnabled => logManager.LogLevel >= LogLevel.SInfo;

        /// <inheritdoc/>
        public bool IsWarnEnabled => logManager.LogLevel >= LogLevel.Warn;

        /// <summary>
        /// Constructs a named instance.
        /// </summary>
        /// <param name="logManager">The parent log manager or <c>null</c>.</param>
        /// <param name="name">The instance name or <c>null</c>.</param>
        /// <remarks>
        /// The instances returned will log nothing if <paramref name="logManager"/>
        /// is passed as <c>null</c>.
        /// </remarks>
        public NeonLogger(ILogManager logManager, string name = null)
        {
            this.logManager = logManager ?? LogManager.Disabled;
            this.name       = name ?? string.Empty;
        }

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel)
        {
            // Map into Neon log levels.

            switch (logLevel)
            {
                case LogLevel.Debug:

                    return IsDebugEnabled;

                case LogLevel.Info:

                    return IsInfoEnabled;

                case LogLevel.Warn:

                    return IsWarnEnabled;

                case LogLevel.Error:

                    return IsErrorEnabled;

                case LogLevel.Critical:

                    return IsCriticalEnabled;

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
        /// <param name="level">The event level.</param>
        /// <param name="message">The event message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        private void Log(string level, string message, string activityId = null)
        {
            message = Normalize(message);

            lock (name)
            {
                var module = string.Empty;

                if (!string.IsNullOrEmpty(name))
                {
                    module = $" [module:{name}]";
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
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00");

                    Console.WriteLine($"[{timestamp}] [{level}]{activity}{module}{index} {message}");
                }
                else
                {
                    Console.WriteLine($"[{level}]{activity}{module}{index} {message}");
                }
            }
        }

        /// <inheritdoc/>
        public void LogDebug(object message, string activityId = null)
        {
            if (IsDebugEnabled)
            {
                try
                {
                    Log("DEBUG", message?.ToString());
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
            if (IsDebugEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log("DEBUG", $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log("DEBUG", $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
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
            if (IsErrorEnabled)
            {
                try
                {
                    Log("ERROR", message?.ToString());
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
            if (IsErrorEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log("ERROR", $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log("ERROR", $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
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
            if (IsErrorEnabled)
            {
                try
                {
                    Log("SERROR", message?.ToString());
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
            if (IsSErrorEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log("SERROR", $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log("SERROR", $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
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
            if (IsCriticalEnabled)
            {
                try
                {
                    Log("CRITICAL", message?.ToString());
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
            if (IsCriticalEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log("CRITICAL", $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log("CRITICAL", $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
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
            if (IsInfoEnabled)
            {
                try
                {
                    Log("INFO", message?.ToString());
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
            if (IsInfoEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log("INFO", $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log("INFO", $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
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
            if (IsInfoEnabled)
            {
                try
                {
                    Log("SINFO", message?.ToString());
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
            if (IsInfoEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log("SINFO", $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log("SINFO", $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
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
            if (IsWarnEnabled)
            {
                try
                {
                    Log("WARN", message?.ToString());
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
            if (IsWarnEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log("WARN", $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log("WARN", $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
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
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return IsEnabled(ToNeonLevel(logLevel));
        }

        /// <inheritdoc/>
        public IDisposable BeginScope<TState>(TState state)
        {
            // We're not doing anything special for this right now.

            return scopeGlobal;
        }
    }
}
