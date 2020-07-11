//-----------------------------------------------------------------------------
// FILE:	    NeonControllerBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;

namespace Neon.Web
{
    /// <summary>
    /// Enhances the <see cref="ControllerBase"/> class to simplify and enhance web service logging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides two logging related enhancements.  First, <see cref="NeonController"/>
    /// implements <see cref="INeonLogger"/> so that all of the standard logging methods are directly
    /// available in the context of the derived controller.  Events will be logged with the module
    /// set to <b>"Web-"</b> prefixing the name of the controller.
    /// </para>
    /// <para>
    /// The <see cref="ActivityId"/> property can also be used to easily correlate operations that
    /// span multiple systems and services.  An activity is a globally unique string that can be
    /// used to corelate a parent operation with any decendent operations.  For example, a parent
    /// operation such as <b>get-weather</b> may need to call several other web services to 
    /// <b>get-current-weather</b>, <b>get-forecast</b>, <b>get-weather-alerts</b>,... and these
    /// child services may need to call other services.  The essential idea here is to generate 
    /// an activity ID for the parent operation, recursively pass this to any child operations and
    /// then include the activity ID in any logged errors or warnings.
    /// </para>
    /// <para>
    /// This can be very useful operationally for diagnosing problems.  A typical scanario is:
    /// a parent operation fails and an error is logged and the operator can then review the
    /// logs with the activity across all systems and services to disgnose exactly what happened.
    /// </para>
    /// <para>
    /// The Neon framework and <b>cluster</b> have built-in mechanisms to make this easy.
    /// <see cref="INeonLogger"/> logging methods include <b>activityId</b> as first class parameters
    /// and the cluster pipeline implicitly process and persist <b>activity-id</b> fields
    /// from event streams.  
    /// </para>
    /// <para>
    /// The <b>neon-proxy-public</b> and <b>neon-proxy-private</b> services are also aware
    /// of activity IDs and will include these in the HTTP traffic logs and also generate
    /// new activity IDs for inbound requests that don't already have them.  This value will
    /// be available as the <see cref="ActivityId"/> property.
    /// </para>
    /// <para>
    /// To enable cross system/service activity correlation, you'll need to include the 
    /// <see cref="ActivityId"/> as the <b>X-Request-ID</b> header in requests made to
    /// those systems.  The <see cref="JsonClient"/> includes built-in methods that make 
    /// this easy.
    /// </para>
    /// </remarks>
    [TypeFilter(typeof(LoggingExceptionFilter))]
    public abstract class NeonControllerBase : ControllerBase, INeonLogger
    {
        private INeonLogger     log;
        private string          activityId;

        /// <summary>
        /// Constructor.
        /// </summary>
        protected NeonControllerBase()
        {
        }

        /// <summary>
        /// <b>Internal use only:</b> Return's the request's <b>X-Request-ID</b> header
        /// value or <c>null</c>.  Application services should use <see cref="ActivityId"/>
        /// which guarantees that a valid activity ID will be returned.
        /// </summary>
        [FromHeader(Name = "X-Request-ID")]
        protected string InternalActivityId { get; set; }

        /// <summary>
        /// Returns the opaque globally unique activity ID for the current operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Activity IDs can be used to correlate information like logs operations across 
        /// multiple systems by high-level operation.  For example, a user request to and
        /// API service may need to be satisified by multiple API requests to other APIs
        /// or services
        /// </para>
        /// <para>
        /// Neon uses the HTTP <b>X-Request-ID</b> header to correlate requests.  This
        /// is an opaque globally unique string.  This is generated automatically by 
        /// <b>neon-proxy-public</b> and <b>neon-proxy-private</b> for inbound HTTP
        /// requests that don't already have this header value.
        /// </para>
        /// <para>
        /// This property always returns a valid activity ID.  This will be the activity
        /// ID header included in the request or a newly generated ID.
        /// </para>
        /// </remarks>
        protected string ActivityId
        {
            get
            {
                if (!string.IsNullOrEmpty(activityId))
                {
                    return activityId;
                }

                // Lazy generate the activity ID if necessary.

                if (string.IsNullOrEmpty(InternalActivityId))
                {
                    activityId = WebHelper.GenerateActivityId();
                }
                else
                {
                    activityId = InternalActivityId;
                }

                return activityId;
            }
        }

        //---------------------------------------------------------------------
        // ILog implementation.

        /// <summary>
        /// Returns the logger to use for this instance.
        /// </summary>
        /// <returns>The logger.</returns>
        private INeonLogger GetLogger()
        {
            // Lazy load the logger for better performance in the common case
            // where nothing is logged for a request.

            if (log != null)
            {
                return log;
            }

            // $todo(jefflill):
            //
            // I should be getting either an [ILogProvider] or [ILogManager] dynamically 
            // via dependency injection rather than hardcoding a call to [LogManager.Default]
            // and then getting an [INeonLogger] from that or wrapping an [ILogger] with
            // a [NeonLoggerShim].
            //
            // I'm not entirely sure how to accomplish this.  I believe the only way is
            // to add a [ILogProvider] parameter to this class' constructor (as well as
            // that of any derived classes) and then inspect the actual instance type
            // passed and then decide whether we need a [NeonLoggerShim] or not.
            //
            // It would be unforunate though to require derived classes to have to handle
            // this.  An alternative might be use property injection, but I don't think
            // the ASP.NET pipeline supports that.
            //
            // See the TODO in [LogManager.cs] for more information.

            return log = LogManager.Default.GetLogger("Web-" + base.ControllerContext.ActionDescriptor.ControllerName);
        }

        /// <summary>
        /// Returns the logger's context ID or <c>null</c>.
        /// </summary>
        public string ContextId => GetLogger().ContextId;

        /// <summary>
        /// Returns <c>true</c> if <b>debug</b> logging is enabled.
        /// </summary>
        public bool IsLogDebugEnabled => GetLogger().IsLogDebugEnabled;

        /// <summary>
        /// Returns <c>true</c> if <b>transient</b> logging is enabled.
        /// </summary>
        public bool IsLogTransientEnabled => GetLogger().IsLogTransientEnabled;

        /// <summary>
        /// Returns <c>true</c> if <b>sinfo</b> logging is enabled.
        /// </summary>
        public bool IsLogSInfoEnabled => GetLogger().IsLogSInfoEnabled;

        /// <summary>
        /// Returns <c>true</c> if <b>info</b> logging is enabled.
        /// </summary>
        public bool IsLogInfoEnabled => GetLogger().IsLogInfoEnabled;

        /// <summary>
        /// Returns <c>true</c> if <b>warn</b> logging is enabled.
        /// </summary>
        public bool IsLogWarnEnabled => GetLogger().IsLogWarnEnabled;

        /// <summary>
        /// Returns <c>true</c> if <b>error</b> logging is enabled.
        /// </summary>
        public bool IsLogErrorEnabled => GetLogger().IsLogErrorEnabled;

        /// <summary>
        /// Returns <c>true</c> if <b>serror</b> logging is enabled.
        /// </summary>
        public bool IsLogSErrorEnabled => GetLogger().IsLogSErrorEnabled;

        /// <summary>
        /// Returns <c>true</c> if <b>critical</b> logging is enabled.
        /// </summary>
        public bool IsLogCriticalEnabled => GetLogger().IsLogCriticalEnabled;

        /// <summary>
        /// Logs a <b>critical</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Critical(object message, string activityId = null)
        {
            GetLogger().LogCritical(message, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>critical</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Critical(object message, Exception e, string activityId = null)
        {
            GetLogger().LogCritical(message, e, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>debug</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Debug(object message, string activityId = null)
        {
            GetLogger().LogDebug(message, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>debug</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Debug(object message, Exception e, string activityId = null)
        {
            GetLogger().LogDebug(message, e, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>transient</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Transient(object message, Exception e, string activityId = null)
        {
            GetLogger().LogTransient(message, e, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>transient</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Transient(object message, string activityId = null)
        {
            GetLogger().LogTransient(message, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs an <b>error</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Error(object message, string activityId = null)
        {
            GetLogger().LogError(message, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs an <b>error</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Error(object message, Exception e, string activityId = null)
        {
            GetLogger().LogError(message, e, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs an <b>info</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Info(object message, string activityId = null)
        {
            GetLogger().LogInfo(message, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs an <b>info</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Info(object message, Exception e, string activityId = null)
        {
            GetLogger().LogInfo(message, e, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>serror</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void SError(object message, string activityId = null)
        {
            GetLogger().LogSError(message, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>serror</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void SError(object message, Exception e, string activityId = null)
        {
            GetLogger().LogSError(message, e, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>sinfo</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void SInfo(object message, string activityId = null)
        {
            GetLogger().LogSInfo(message, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>sinfo</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void SInfo(object message, Exception e, string activityId = null)
        {
            GetLogger().LogSInfo(message, e, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>warn</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Warn(object message, string activityId = null)
        {
            GetLogger().LogWarn(message, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Logs a <b>warn</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">Optional activity ID.</param>
        public void Warn(object message, Exception e, string activityId = null)
        {
            GetLogger().LogWarn(message, e, activityId ?? this.ActivityId);
        }

        /// <summary>
        /// Indicates whether logging is enabled for a specific log level.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns><c>true</c> if logging is enabled for <paramref name="logLevel"/>.</returns>
        public bool IsLogLevelEnabled(LogLevel logLevel)
        {
            return GetLogger().IsLogLevelEnabled(logLevel);
        }

        /// <summary>
        /// Logs a <b>debug</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogDebug(object message, string activityId = null)
        {
            GetLogger().LogDebug(message, activityId);
        }

        /// <summary>
        /// Logs a <b>transient</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogTransient(object message, string activityId = null)
        {
            GetLogger().LogTransient(message, activityId);
        }

        /// <summary>
        /// Logs an <b>sinfo</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogSInfo(object message, string activityId = null)
        {
            GetLogger().LogSInfo(message, activityId);
        }

        /// <summary>
        /// Logs an <b>info</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogInfo(object message, string activityId = null)
        {
            GetLogger().LogInfo(message, activityId);
        }

        /// <summary>
        /// Logs a <b>warn</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogWarn(object message, string activityId = null)
        {
            GetLogger().LogWarn(message, activityId);
        }

        /// <summary>
        /// Logs an <b>serror</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogSError(object message, string activityId = null)
        {
            GetLogger().LogSError(message, activityId);
        }

        /// <summary>
        /// Logs an <b>error</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogError(object message, string activityId = null)
        {
            GetLogger().LogError(message, activityId);
        }

        /// <summary>
        /// Logs a <b>critical</b> message.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogCritical(object message, string activityId = null)
        {
            GetLogger().LogCritical(message, activityId);
        }

        /// <summary>
        /// Logs a <b>debug</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogDebug(object message, Exception e, string activityId = null)
        {
            GetLogger().LogDebug(message, e, activityId);
        }

        /// <summary>
        /// Logs a <b>transient</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogTransient(object message, Exception e, string activityId = null)
        {
            GetLogger().LogTransient(message, e, activityId);
        }

        /// <summary>
        /// Logs an <b>sinfo</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogSInfo(object message, Exception e, string activityId = null)
        {
            GetLogger().LogSInfo(message, e, activityId);
        }

        /// <summary>
        /// Logs an <b>info</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogInfo(object message, Exception e, string activityId = null)
        {
            GetLogger().LogInfo(message, e, activityId);
        }

        /// <summary>
        /// Logs a <b>warn</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogWarn(object message, Exception e, string activityId = null)
        {
            GetLogger().LogWarn(message, e, activityId);
        }

        /// <summary>
        /// Logs an <b>error</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogError(object message, Exception e, string activityId = null)
        {
            GetLogger().LogError(message, e, activityId);
        }

        /// <summary>
        /// Logs an <b>serror</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogSError(object message, Exception e, string activityId = null)
        {
            GetLogger().LogSError(message, e, activityId);
        }

        /// <summary>
        /// Logs a <b>critical</b> message along with exception information.
        /// </summary>
        /// <param name="message">The object that will be serialized into the message.</param>
        /// <param name="e">The exception.</param>
        /// <param name="activityId">The optional activity ID.</param>
        public void LogCritical(object message, Exception e, string activityId = null)
        {
            GetLogger().LogCritical(message, e, activityId);
        }
    }
}
