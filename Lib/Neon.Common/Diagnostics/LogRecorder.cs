//-----------------------------------------------------------------------------
// FILE:	    LogRecorder.cs
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
    /// Simple class that can be used to capture log entries while also passing them
    /// through to a base <see cref="ILog"/> implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instantiate by passing a base <see cref="ILog"/> implementation to the constructor.
    /// All of the logging properties and methods calls to this class will pass through
    /// to the base implementation and enabled log methods will also be collected by the
    /// instance.  Use <see cref="ToString"/> to return the captured text and <see cref="Clear"/>
    /// to clear it.
    /// </para>
    /// </remarks>
    public class LogRecorder : ILog
    {
        private ILog            log;
        private StringBuilder   capture;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="log">The inderlying <see cref="ILog"/> implementation.</param>
        public LogRecorder(ILog log)
        {
            Covenant.Requires<ArgumentNullException>(log != null);

            this.log     = log;
            this.capture = new StringBuilder();
        }

        /// <summary>
        /// Clears the captured text.
        /// </summary>
        public void Clear()
        {
            lock (capture)
            {
                capture.Clear();
            }
        }

        /// <summary>
        /// Returns the captured log text.
        /// </summary>
        /// <returns>The text.</returns>
        public override string ToString()
        {
            lock (capture)
            {
                return capture.ToString();
            }
        }

        /// <inheritdoc/>
        public bool IsDebugEnabled => log.IsDebugEnabled;

        /// <inheritdoc/>
        public bool IsInfoEnabled => log.IsInfoEnabled;

        /// <inheritdoc/>
        public bool IsWarnEnabled => log.IsWarnEnabled;

        /// <inheritdoc/>
        public bool IsErrorEnabled => log.IsErrorEnabled;

        /// <inheritdoc/>
        public bool IsFatalEnabled => log.IsFatalEnabled;

        /// <inheritdoc/>
        public void Debug(object message, string activityId = null)
        {
            log.Debug(message, activityId);
            capture.AppendLine($"[DEBUG] {message}");
        }

        /// <inheritdoc/>
        public void Info(object message, string activityId = null)
        {
            log.Info(message, activityId);
            capture.AppendLine($"[INFO] {message}");
        }

        /// <inheritdoc/>
        public void Warn(object message, string activityId = null)
        {
            log.Warn(message, activityId);
            capture.AppendLine($"[WARN]: {message}");
        }

        /// <inheritdoc/>
        public void Error(object message, string activityId = null)
        {
            log.Error(message, activityId);
            capture.AppendLine($"[ERROR] {message}");
        }

        /// <inheritdoc/>
        public void Fatal(object message, string activityId = null)
        {
            log.Fatal(message, activityId);
            capture.AppendLine($"[FATAL] {message}");
        }

        /// <inheritdoc/>
        public void Debug(object message, Exception e, string activityId = null)
        {
            log.Debug(message, e, activityId);
            capture.AppendLine($"[DEBUG] {message} {NeonHelper.ExceptionError(e)}");
        }

        /// <inheritdoc/>
        public void Info(object message, Exception e, string activityId = null)
        {
            log.Info(message, e, activityId);
            capture.AppendLine($"[INFO] {message} {NeonHelper.ExceptionError(e)}");
        }

        /// <inheritdoc/>
        public void Warn(object message, Exception e, string activityId = null)
        {
            log.Warn(message, e, activityId);
            capture.AppendLine($"[WARN] {message} {NeonHelper.ExceptionError(e)}");
        }

        /// <inheritdoc/>
        public void Error(object message, Exception e, string activityId = null)
        {
            log.Error(message, e, activityId);
            capture.AppendLine($"[ERROR] {message} {NeonHelper.ExceptionError(e)}");
        }

        /// <inheritdoc/>
        public void Fatal(object message, Exception e, string activityId = null)
        {
            log.Fatal(message, e, activityId);
            capture.AppendLine($"[FATAL] {message} {NeonHelper.ExceptionError(e)}");
        }

        /// <summary>
        /// Logs a line of text directly to the log recorder without also logging it to
        /// to the underlying <see cref="ILog"/> implementation.
        /// </summary>
        /// <param name="message">The optional message to be recorded.</param>
        public void Record(string message = null)
        {
            capture.AppendLine(message ?? string.Empty);
        }
    }
}
