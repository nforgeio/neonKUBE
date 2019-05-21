//-----------------------------------------------------------------------------
// FILE:	    LogExtensions.cs
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Diagnostics
{
    /// <summary>
    /// Extends the <see cref="INeonLogger"/> types.
    /// </summary>
    public static class LogExtensions
    {
        /// <summary>
        /// Logs a debug message retrieved via a message function.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="messageFunc">The message function.</param>
        /// <remarks>
        /// This method is intended mostly to enable the efficient use of interpolated C# strings.
        /// </remarks>
        public static void LogDebug(this INeonLogger log, Func<object> messageFunc)
        {
            if (log.IsLogDebugEnabled)
            {
                log.LogDebug(messageFunc());
            }
        }

        /// <summary>
        /// Logs an informational message retrieved via a message function.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="messageFunc">The message function.</param>
        /// <remarks>
        /// This method is intended mostly to enable the efficient use of interpolated C# strings.
        /// </remarks>
        public static void LogInfo(this INeonLogger log, Func<object> messageFunc)
        {
            if (log.IsLogInfoEnabled)
            {
                log.LogInfo(messageFunc());
            }
        }

        /// <summary>
        /// Logs a warning message retrieved via a message function.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="messageFunc">The message function.</param>
        /// <remarks>
        /// This method is intended mostly to enable the efficient use of interpolated C# strings.
        /// </remarks>
        public static void LogWarn(this INeonLogger log, Func<object> messageFunc)
        {
            if (log.IsLogWarnEnabled)
            {
                log.LogWarn(messageFunc());
            }
        }

        /// <summary>
        /// Logs an error message retrieved via a message function.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="messageFunc">The message function.</param>
        /// <remarks>
        /// This method is intended mostly to enable the efficient use of interpolated C# strings.
        /// </remarks>
        public static void LogError(this INeonLogger log, Func<object> messageFunc)
        {
            if (log.IsLogErrorEnabled)
            {
                log.LogError(messageFunc());
            }
        }

        /// <summary>
        /// Logs a critical message retrieved via a message function.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="messageFunc">The message function.</param>
        /// <remarks>
        /// This method is intended mostly to enable the efficient use of interpolated C# strings.
        /// </remarks>
        public static void LogCritical(this INeonLogger log, Func<object> messageFunc)
        {
            if (log.IsLogCriticalEnabled)
            {
                log.LogCritical(messageFunc());
            }
        }

        /// <summary>
        /// Logs a debug exception.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="e">The exception.</param>
        public static void LogDebug(this INeonLogger log, Exception e)
        {
            if (log.IsLogDebugEnabled)
            {
                log.LogDebug(null, e);
            }
        }

        /// <summary>
        /// Logs an info exception.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="e">The exception.</param>
        public static void LogInfo(this INeonLogger log, Exception e)
        {
            if (log.IsLogInfoEnabled)
            {
                log.LogInfo(null, e);
            }
        }

        /// <summary>
        /// Logs a warning exception.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="e">The exception.</param>
        public static void LogWarn(this INeonLogger log, Exception e)
        {
            if (log.IsLogWarnEnabled)
            {
                log.LogWarn(null, e);
            }
        }

        /// <summary>
        /// Logs an error exception.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="e">The exception.</param>
        public static void LogError(this INeonLogger log, Exception e)
        {
            if (log.IsLogErrorEnabled)
            {
                log.LogError(null, e);
            }
        }

        /// <summary>
        /// Logs a critical exception.
        /// </summary>
        /// <param name="log">The log.</param>
        /// <param name="e">The exception.</param>
        public static void LogCritical(this INeonLogger log, Exception e)
        {
            if (log.IsLogCriticalEnabled)
            {
                log.LogCritical(null, e);
            }
        }
    }
}
