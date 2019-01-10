//-----------------------------------------------------------------------------
// FILE:        RecurringTimerType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace Neon.Time
{
    /// <summary>
    /// Enumerates the possible <see cref="RecurringTimer" /> types.
    /// </summary>
    public enum RecurringTimerType
    {
        /// <summary>
        /// The timer never fires.
        /// </summary>
        Disabled,

        /// <summary>
        /// The timer will be fired once per minute. 
        /// </summary>
        Minute,

        /// <summary>
        /// The timer will be fired once every 15 minutes.
        /// </summary>
        QuarterHour,

        /// <summary>
        /// The timer will be fired once per hour.
        /// </summary>
        Hourly,

        /// <summary>
        /// The timer will be fired once per day.
        /// </summary>
        Daily,

        /// <summary>
        /// The timer is fired on a specified interval rather than a 
        /// specific period offset.  This is similar to how <see cref="PolledTimer" />
        /// works.
        /// </summary>
        Interval
    }
}
