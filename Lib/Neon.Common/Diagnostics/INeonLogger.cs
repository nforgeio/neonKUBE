//-----------------------------------------------------------------------------
// FILE:	    INeonLogger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
    /// Defines the methods and properties for a diagnostics logger. 
    /// </summary>
    public interface INeonLogger
    {
        /// <summary>
        /// Returns the logger's context ID or <c>null</c>.
        /// </summary>
        string ContextId { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>debug</b> logging is enabled.
        /// </summary>
        bool IsLogDebugEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>transient</b> logging is enabled.
        /// </summary>
        bool IsLogTransientEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>sinfo</b> logging is enabled.
        /// </summary>
        bool IsLogSInfoEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>info</b> logging is enabled.
        /// </summary>
        bool IsLogInfoEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>warn</b> logging is enabled.
        /// </summary>
        bool IsLogWarnEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>error</b> logging is enabled.
        /// </summary>
        bool IsLogErrorEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>serror</b> logging is enabled.
        /// </summary>
        bool IsLogSErrorEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>critical</b> logging is enabled.
        /// </summary>
        bool IsLogCriticalEnabled { get; }

        /// <summary>
        /// Indicates whether logging is enabled for a specific log level.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns><c>true</c> if logging is enabled for <paramref name="logLevel"/>.</returns>
        bool IsLogLevelEnabled(LogLevel logLevel);

        /// <summary>
        /// Logs a <b>debug</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogDebug(object message, string activityId = null);

        /// <summary>
        /// Logs a <b>transient</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogTransient(object message, string activityId = null);

        /// <summary>
        /// Logs an <b>sinfo</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogSInfo(object message, string activityId = null);

        /// <summary>
        /// Logs an <b>info</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogInfo(object message, string activityId = null);

        /// <summary>
        /// Logs a <b>warn</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogWarn(object message, string activityId = null);

        /// <summary>
        /// Logs an <b>serror</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogSError(object message, string activityId = null);

        /// <summary>
        /// Logs an <b>error</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogError(object message, string activityId = null);

        /// <summary>
        /// Logs a <b>critical</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogCritical(object message, string activityId = null);

        /// <summary>
        /// Logs a <b>debug</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogDebug(object message, Exception e, string activityId = null);

        /// <summary>
        /// Logs a <b>transient</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogTransient(object message, Exception e, string activityId = null);

        /// <summary>
        /// Logs an <b>sinfo</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogSInfo(object message, Exception e, string activityId = null);

        /// <summary>
        /// Logs an <b>info</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogInfo(object message, Exception e, string activityId = null);

        /// <summary>
        /// Logs a <b>warn</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogWarn(object message, Exception e, string activityId = null);

        /// <summary>
        /// Logs an <b>error</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogError(object message, Exception e, string activityId = null);

        /// <summary>
        /// Logs an <b>serror</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogSError(object message, Exception e, string activityId = null);

        /// <summary>
        /// Logs a <b>critical</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        void LogCritical(object message, Exception e, string activityId = null);
    }
}
