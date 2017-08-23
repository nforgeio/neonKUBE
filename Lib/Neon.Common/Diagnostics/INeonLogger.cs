//-----------------------------------------------------------------------------
// FILE:	    INeonLogger.cs
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
        bool IsDebugEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>sinfo</b> logging is enabled.
        /// </summary>
        bool IsSInfoEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>info</b> logging is enabled.
        /// </summary>
        bool IsInfoEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>warn</b> logging is enabled.
        /// </summary>
        bool IsWarnEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>error</b> logging is enabled.
        /// </summary>
        bool IsErrorEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>serror</b> logging is enabled.
        /// </summary>
        bool IsSErrorEnabled { get; }

        /// <summary>
        /// Returns <c>true</c> if <b>critical</b> logging is enabled.
        /// </summary>
        bool IsCriticalEnabled { get; }

        /// <summary>
        /// Indicates whether logging is enabled for a specific log level.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns><c>true</c> if logging is enabled for <paramref name="logLevel"/>.</returns>
        bool IsEnabled(LogLevel logLevel);

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
    }
}
