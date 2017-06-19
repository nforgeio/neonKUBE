//-----------------------------------------------------------------------------
// FILE:	    Logger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Diagnostics
{
    /// <summary>
    /// A general purpose implementation of <see cref="ILog"/> .
    /// </summary>
    internal class Logger : ILog
    {
        private string name;

        /// <inheritdoc/>
        public bool IsDebugEnabled { get; internal set; } = LogManager.LogLevel >= LogLevel.Debug;

        /// <inheritdoc/>
        public bool IsErrorEnabled { get; internal set; } = LogManager.LogLevel >= LogLevel.Error;

        /// <inheritdoc/>
        public bool IsFatalEnabled { get; internal set; } = LogManager.LogLevel >= LogLevel.Fatal;

        /// <inheritdoc/>
        public bool IsInfoEnabled { get; internal set; }  = LogManager.LogLevel >= LogLevel.Info;

        /// <inheritdoc/>
        public bool IsWarnEnabled { get; internal set; }  = LogManager.LogLevel >= LogLevel.Warn;

        /// <summary>
        /// Constructs a named instance.
        /// </summary>
        /// <param name="name">The instance name or <c>null</c>.</param>
        public Logger(string name = null)
        {
            this.name = name ?? "[unknown]";
        }

        /// <summary>
        /// Normalizes a log message by escaping any backslashes and replacing any line
        /// endings with "\n".  This converts multi-line message to a single line.s
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

                if (LogManager.EmitTimestamp)
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00");

                    Console.WriteLine($"[{timestamp}] [{level}]{activity}{module} {message}");
                }
                else
                {
                    Console.WriteLine($"[{level}]{activity}{module} {message}");
                }
            }
        }

        /// <inheritdoc/>
        public void Debug(object message, string activityId = null)
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
        public void Debug(object message, Exception e, string activityId = null)
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
        public void Error(object message, string activityId = null)
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
        public void Error(object message, Exception e, string activityId = null)
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
        public void Fatal(object message, string activityId = null)
        {
            if (IsFatalEnabled)
            {
                try
                {
                    Log("FATAL", message?.ToString());
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void Fatal(object message, Exception e, string activityId = null)
        {
            if (IsFatalEnabled)
            {
                try
                {
                    if (message != null)
                    {
                        Log("FATAL", $"{message} {NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                    else
                    {
                        Log("FATAL", $"{NeonHelper.ExceptionError(e, stackTrace: true)}");
                    }
                }
                catch
                {
                    // Doesn't make sense to handle this.
                }
            }
        }

        /// <inheritdoc/>
        public void Info(object message, string activityId = null)
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
        public void Info(object message, Exception e, string activityId = null)
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
        public void Warn(object message, string activityId = null)
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
        public void Warn(object message, Exception e, string activityId = null)
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
    }
}
