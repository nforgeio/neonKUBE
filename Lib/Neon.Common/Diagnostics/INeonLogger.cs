//-----------------------------------------------------------------------------
// FILE:	    INeonLogger.cs
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
    /// Defines the methods and properties for a diagnostics logger. 
    /// </summary>
    public interface INeonLogger
    {
        /// <summary>
        /// Returns <c>true</c> if <b>debug</b> logging is enabled.
        /// </summary>
        bool IsLogDebugEnabled { get; }

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

        /// <summary>
        /// Logs text and numeric metrics.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="textFields">The text fields (or <c>null</c>).</param>
        /// <param name="numFields">The numeric fields (or <c>null</c>).</param>
        /// <exception cref="ArgumentException">
        /// Thrown if either of <paramref name="textFields"/> or <paramref name="numFields"/> 
        /// includes more than 10 items.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This can be used to log text and/or numeric metrics in a log event.
        /// Text fields will be written to the logged event like <b>[txt.#=VALUE]</b>
        /// where <b>#</b> is the field index [0-9].  Numeric fields will be
        /// written as <b>[num.#=VALUE]</b>.
        /// </para>
        /// <para>
        /// The <b>neon-log-collector</b> service will recognizes these and
        /// persist them as <b>txt.#=VALUE</b> and <b>num.#=VALUE</b> fields
        /// in the Elasticsearch <b>logstash-*</b> index.
        /// </para>
        /// <note>
        /// Up to 10 of each type of metric can be passed.
        /// </note>
        /// </remarks>
        void LogMetrics(LogLevel level, IEnumerable<string> textFields, IEnumerable<double> numFields);

        /// <summary>
        /// Logs text metrics.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="textFields">The text fields (or <c>null</c>).</param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="textFields"/> includes more than 10 items.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This can be used to log text metrics in a log event.  Text fields will 
        /// be written to the logged event like <b>[txt.#=VALUE]</b> where
        /// <b>#</b> is the field index [0-9].
        /// </para>
        /// <para>
        /// The <b>neon-log-collector</b> service will recognizes these and
        /// persist them as <b>txt.#=VALUE</b> fields in the Elasticsearch 
        /// <b>logstash-*</b> index.
        /// </para>
        /// <note>
        /// Up to 10 metrics can be passed.
        /// </note>
        /// </remarks>
        void LogMetrics(LogLevel level, params string[] textFields);

        /// <summary>
        /// Logs numeric metrics.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="numFields">The numeric fields (or <c>null</c>).</param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="numFields"/> includes more than 10 items.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This can be used to log numeric metrics in a log event.  Numeric fields
        /// will  be written to the logged event like <b>[num.#=VALUE]</b> where
        /// <b>#</b> is the field index [0-9].
        /// </para>
        /// <para>
        /// The <b>neon-log-collector</b> service will recognizes these and
        /// persist them as <b>num.#=VALUE</b> fields in the Elasticsearch 
        /// <b>logstash-*</b> index.
        /// </para>
        /// <note>
        /// Up to 10 metrics can be passed.
        /// </note>
        /// </remarks>
        void LogMetrics(LogLevel level, params double[] numFields);
    }
}
