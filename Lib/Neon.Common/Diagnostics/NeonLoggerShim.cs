//-----------------------------------------------------------------------------
// FILE:	    NeonLoggerShim.cs
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
    /// Wraps a standard <see cref="ILogger"/> adding <see cref="INeonLogger"/> capabilities.
    /// </summary>
    public class NeonLoggerShim : ILogger, INeonLogger
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Converts an <see cref="ILogger"/> into an instance that implements <see cref="INeonLogger"/>,
        /// wrapping the instance passed with a <see cref="NeonLoggerShim"/> if required.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to be converted.</param>
        /// <param name="writer">Optionally specifies the output writer.  This defaults to <see cref="Console.Error"/>.</param>
        /// <returns>The <see cref="INeonLogger"/>.</returns>
        /// <remarks>
        /// This method will return a <b>log-nothing</b> instance is <paramref name="logger"/> is <c>null</c>.
        /// </remarks>
        public static INeonLogger WrapLogger(ILogger logger, TextWriter writer = null)
        {
            if (logger == null)
            {
                return new NeonLogger(null, writer: writer);
            }

            return new NeonLoggerShim(logger);
        }

        //---------------------------------------------------------------------
        // Instance members

        private ILogger logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to be wrapped.</param>
        public NeonLoggerShim(ILogger logger)
        {
            Covenant.Requires<ArgumentNullException>(logger == null);

            this.logger = logger;
        }

        //---------------------------------------------------------------------
        // INeonLogger implementation:

        /// <inheritdoc/>
        public bool IsLogDebugEnabled => logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug);

        /// <inheritdoc/>
        public bool IsLogErrorEnabled => logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Error);

        /// <inheritdoc/>
        public bool IsLogSErrorEnabled => logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Error);

        /// <inheritdoc/>
        public bool IsLogCriticalEnabled => logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Critical);

        /// <inheritdoc/>
        public bool IsLogInfoEnabled => logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information);

        /// <inheritdoc/>
        public bool IsLogSInfoEnabled => logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information);

        /// <inheritdoc/>
        public bool IsLogWarnEnabled => logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning);

        /// <inheritdoc/>
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logger.IsEnabled(logLevel);
        }

        /// <inheritdoc/>
        public bool IsLogLevelEnabled(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.None:     return false;
                case LogLevel.Critical: return IsLogCriticalEnabled;
                case LogLevel.Debug:    return IsLogDebugEnabled;
                case LogLevel.Error:    return IsLogErrorEnabled;
                case LogLevel.Info:     return IsLogInfoEnabled;
                case LogLevel.SError:   return IsLogSErrorEnabled;
                case LogLevel.SInfo:    return IsLogSInfoEnabled;
                case LogLevel.Warn:     return IsLogWarnEnabled;
                default:                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Normalizes a log message by escaping any backslashes and replacing any line
        /// endings with "\n".  This converts multi-line message to a single line.
        /// </summary>
        /// <param name="text">The message text.</param>
        /// <returns>The normalized message.</returns>
        private static string Normalize(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            text = text.Replace("\\", "\\\\");
            text = text.Replace("\r\n", "\\n");
            text = text.Replace("\n", "\\n");

            return text;
        }

        /// <summary>
        /// Formats the log message.
        /// </summary>
        /// <param name="message">The message object.</param>
        /// <param name="activityId">The optional activiity ID.</param>
        /// <returns></returns>
        private static string FormatMessage(object message, string activityId = null)
        {
            string text;

            if (message == null)
            {
                text = string.Empty;
            }
            else
            {
                text = message.ToString();
            }

            if (!string.IsNullOrEmpty(activityId))
            {
                text += $" [activity-id:{activityId}]";
            }

            return text;
        }

        /// <inheritdoc/>
        public void LogDebug(object message, string activityId = null)
        {
            logger.LogDebug(FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, string activityId = null)
        {
            logger.LogInformation(FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogInfo(object message, string activityId = null)
        {
            logger.LogInformation(FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogWarn(object message, string activityId = null)
        {
            logger.LogWarning(FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogSError(object message, string activityId = null)
        {
            logger.LogError(FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogError(object message, string activityId = null)
        {
            logger.LogError(FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogCritical(object message, string activityId = null)
        {
            logger.LogCritical(FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogDebug(object message, Exception e, string activityId = null)
        {
            logger.LogDebug(e, FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, Exception e, string activityId = null)
        {
            logger.LogInformation(e, FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogInfo(object message, Exception e, string activityId = null)
        {
            logger.LogInformation(e, FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogWarn(object message, Exception e, string activityId = null)
        {
            logger.LogWarning(e, FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogError(object message, Exception e, string activityId = null)
        {
            logger.LogError(e, FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogSError(object message, Exception e, string activityId = null)
        {
            logger.LogError(e, FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogCritical(object message, Exception e, string activityId = null)
        {
            logger.LogCritical(e, FormatMessage(message, activityId));
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, IEnumerable<string> txtFields, IEnumerable<double> numFields)
        {
            var txtCount = 0;
            var numCount = 0;

            if (txtFields != null)
            {
                txtCount = txtFields.Count();
            }

            if (numFields != null)
            {
                numCount = numFields.Count();
            }

            if (txtCount == 0 && numCount == 0)
            {
                return;     // Nothing to write.
            }

            var message = NeonLogger.FormatMetrics(txtFields, numFields);

            switch (level)
            {
                case LogLevel.Critical:

                    LogCritical(message);
                    break;

                case LogLevel.Debug:

                    LogDebug(message);
                    break;

                case LogLevel.Error:

                    LogError(message);
                    break;

                case LogLevel.Info:

                    LogInfo(message);
                    break;

                case LogLevel.None:

                    break;  // NOP

                case LogLevel.SError:

                    LogSError(message);
                    break;

                case LogLevel.SInfo:

                    LogSInfo(message);
                    break;

                case LogLevel.Warn:

                    LogWarn(message);
                    break;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params string[] txtFields)
        {
            LogMetrics(level, txtFields, null);
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params double[] numFields)
        {
            LogMetrics(level, null, numFields);
        }

        //---------------------------------------------------------------------
        // ILogger implementation:

        /// <inheritdoc/>
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            logger.Log<TState>(logLevel, eventId, state, exception, formatter);
        }

        /// <inheritdoc/>
        public IDisposable BeginScope<TState>(TState state)
        {
            return logger.BeginScope<TState>(state);
        }
    }
}
