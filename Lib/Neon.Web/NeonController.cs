//-----------------------------------------------------------------------------
// FILE:	    NeonController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
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
    /// Enhances the base <see cref="Controller"/> class to simplify and enhance WebAPI service logging.
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
    /// The Neon framework and <b>neonHIVE</b> have built-in mechanisms to make this easy.
    /// <see cref="INeonLogger"/> logging methods include <b>activityId</b> as first class parameters
    /// and the neonHIVE pipeline implicitly process and persist <b>activity-id</b> fields
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
    public abstract class NeonController : Controller
    {
        private INeonLogger     log;
        private string          activityId;

        /// <summary>
        /// Default constructor.
        /// </summary>
        protected NeonController()
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

            // $todo(jeff.lill):
            //
            // I should be getting either an [ILogProvider] or [ILogManager] dynamically via 
            // dependency injection rather than hardcoding a call to [LogManager.Default]
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

        /// <inheritdoc/>
        protected bool IsDebugEnabled => GetLogger().IsDebugEnabled;

        /// <inheritdoc/>
        protected bool IsSInfoEnabled => GetLogger().IsSInfoEnabled;

        /// <inheritdoc/>
        protected bool IsInfoEnabled => GetLogger().IsInfoEnabled;

        /// <inheritdoc/>
        protected bool IsWarnEnabled => GetLogger().IsWarnEnabled;

        /// <inheritdoc/>
        protected bool IsErrorEnabled => GetLogger().IsErrorEnabled;

        /// <inheritdoc/>
        protected bool IsSErrorEnabled => GetLogger().IsSErrorEnabled;

        /// <inheritdoc/>
        protected bool IsCriticalEnabled => GetLogger().IsCriticalEnabled;

        /// <inheritdoc/>
        protected void Critical(object message, string activityId = null)
        {
            GetLogger().LogCritical(message, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Critical(object message, Exception e, string activityId = null)
        {
            GetLogger().LogCritical(message, e, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Debug(object message, string activityId = null)
        {
            GetLogger().LogDebug(message, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Debug(object message, Exception e, string activityId = null)
        {
            GetLogger().LogDebug(message, e, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Error(object message, string activityId = null)
        {
            GetLogger().LogError(message, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Error(object message, Exception e, string activityId = null)
        {
            GetLogger().LogError(message, e, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Info(object message, string activityId = null)
        {
            GetLogger().LogInfo(message, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Info(object message, Exception e, string activityId = null)
        {
            GetLogger().LogInfo(message, e, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void SError(object message, string activityId = null)
        {
            GetLogger().LogSError(message, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void SError(object message, Exception e, string activityId = null)
        {
            GetLogger().LogSError(message, e, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void SInfo(object message, string activityId = null)
        {
            GetLogger().LogSInfo(message, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void SInfo(object message, Exception e, string activityId = null)
        {
            GetLogger().LogSInfo(message, e, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Warn(object message, string activityId = null)
        {
            GetLogger().LogWarn(message, activityId ?? this.ActivityId);
        }

        /// <inheritdoc/>
        protected void Warn(object message, Exception e, string activityId = null)
        {
            GetLogger().LogWarn(message, e, activityId ?? this.ActivityId);
        }
    }
}
