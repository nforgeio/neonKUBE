//-----------------------------------------------------------------------------
// FILE:	    SysTime.cs
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
using System.Threading;

// $todo(jeff.lill):
//
// This class uses [Environment.TickCount] to measure time from system boot.
// There are two problems with this:
//
//      1. TickCount appears to have a resolution of only 500ms.  This
//         will be problematic for applications that need a finer resolution.
//
//      2. TickCount returns the milliseconds from boot as an unsigned 32-bit
//         integer.  This means the value will wrap-around after 49.7 days.
//         The class detects this wrap-around but this isn't super clean.
//
//         For Windows, we could PInvoke to get the GetTickCount64() method.
//         We'd need to figure out an alternative for Linux.

namespace Neon.Time
{
    /// <summary>
    /// A date/time implementation that is guaranteed to be monotonically increasing
    /// even as the underlying system time is updated manually or automatically to adjust
    /// for daylight savings time or clock skewing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use of system time rather than real-time is useful in situations
    /// where events need to be timed in relative rather than absolute time.
    /// Using absolute time to measure intervals call can be problematic
    /// because system clock may be have to be periodically corrected to
    /// keep it in sync with a global time base.  These corrections will
    /// cause event timers to become inaccurate.
    /// </para>
    /// <para>
    /// The <see cref="Now" /> property returns the current system time and
    /// <see cref="Infinite" /> calculates and returns an essentially infinite
    /// timespan value that will be safe when added to the current system time.
    /// </para>
    /// <note>
    /// The <see cref="DateTime" /> instances returned by this class are useful
    /// only for measuring timespans.  The Day, Month, Year properties 
    /// will have no useful meaning.
    /// </note>
    /// <note>
    /// The class is implemented such that the first time
    /// returned by the <see cref="Now" /> property will be a time value that 
    /// is a minumim of one year after <see cref="DateTime.MinValue" />.  This is 
    /// useful in situations where programs want to schedule a periodic event 
    /// for immediate triggering when the application starts by setting the last trigger
    /// time to <see cref="DateTime.MinValue" />.
    /// </note>
    /// <para>
    /// The <see cref="SysTime" /> class is also capable of maintaining rough
    /// synchronization with an external time source.  To use this feature,
    /// you'll periodically get the time from the external source and assign
    /// it to the static <see cref="ExternalNow" /> property then you can
    /// use the <see cref="ExternalNow" /> property to get and estimate of the
    /// current external time.
    /// </para>
    /// <para>
    /// The local side clock will likely drift over time, resulting in a skew
    /// between the time returned by <see cref="ExternalNow" /> and the actual
    /// time at the external source.  This skew can be limited by getting the
    /// external time and assigning it to <see cref="ExternalNow" /> more
    /// frequently.
    /// </para>
    /// <note>
    /// The time returned by <see cref="ExternalNow" /> is not guaranteed to be
    /// monotimically increasing since reported times may jump around as the
    /// bias between the local and external clocks are adjusted.
    /// </note>
    /// <para><b><u>Implementation Note</u></b></para>
    /// <para>
    /// This class is currently implemented using the Windows <b>GetTickCount()</b> API.
    /// This function returns the number of milliseconds since the operating system
    /// was booted with a resolution equal to the process/threading timeslice (typically
    /// 10-15 milliseconds).  The actual resolution for the current machine can be
    /// obtained from <see cref="Resolution" />.
    /// </para>
    /// <para>
    /// The <see cref="Environment.TickCount"/> counter is an unsigned 32-bit value and will wrap-around
    /// every 49.7 days.  The <see cref="SysTime" /> class handles this by using a <see cref="GatedTimer" />
    /// to wake up every five minutes to check for and handle this wrap-around.
    /// </para>
    /// <para>
    /// As noted above, the time value returned by <see cref="Now" /> has no relation
    /// to the actual calendar date.  The first date returned after booting the computer
    /// will be approximately 1/1/0002 00:00:00, one year <i>greater</i> the minimum <see cref="DateTime" />
    /// value.  <see cref="Infinite" /> returns a calculated value that when added to <see cref="Now" />
    /// will result in a date one year less then the maximum <see cref="DateTime" /> value.
    /// </para>
    /// <para>
    /// These one year offsets were choosen so that applications can perform reasonable
    /// offset calculations (e.g. within background tasks) without fear of wrap-around.
    /// Since <see cref="DateTime" /> and <see cref="TimeSpan" /> span up to 10,000
    /// years, this means that <see cref="SysTime" /> calculations will remain valid
    /// for up to 9,998 years after the computer has been started, which should be
    /// good enough for most applications.
    /// </para>
    /// </remarks>
    public static class SysTime
    {
        //---------------------------------------------------------------------
        // Windows Implementation
        //
        // I'm going to implement this by using the Windows GetTickCount() API.
        // The problem is that this will wrap around after about 49.7 days.
        // To handle this, I'm going to periodically poll GetTickCount() every
        // 5 minutes, looking for a new count that is less than a previous
        // count, indicating that the counter overflowed.

        private static object       syncLock = new object();
        private static uint         lastCount;      // The last GetTickCount() polled
        private static ulong        msi;            // Most-significant int
        private static GatedTimer   timer;          // The poll timer
        private static TimeSpan     resolution;     // The timer resolution for this machine
        private static TimeSpan     year;           // Approximate year timspan
        private static TimeSpan     externalBias;   // Delta between external and local UTC clocks

        //---------------------------------------------------------------------
        // Unix/Linux Implementation
        //
        // I'm simply going to use [DateTime.Utc] now for Unix/Linux for now, until
        // I can figure out something better.

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SysTime()
        {
            externalBias = TimeSpan.Zero;
            timer        = new GatedTimer(new TimerCallback(OnTimer), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            lastCount    = (uint)Environment.TickCount;
            year         = TimeSpan.FromDays(365);

            // Initialize this such that the first time returned by Now
            // will be quite a long time after DateTime.MinValue.

            msi  = (ulong)(TimeSpan.TicksPerDay * 365 / TimeSpan.TicksPerMillisecond);
            msi &= 0xFFFFFFFF00000000;

            // Get the timer resolution for the current machine.

            resolution = TimeSpan.FromMilliseconds(15);     // $hack(jeff.lill): This is a guess
        }

        /// <summary>
        /// Used by Unit tests to reset the timer class to its initial value.
        /// </summary>
        static public void Reset() 
        {
            lastCount = (uint) Environment.TickCount;

            // Initialize this such that the first time returned by Now
            // will be quite a long time after DateTime.MinValue.

            msi  = (ulong) (TimeSpan.TicksPerDay*365 / TimeSpan.TicksPerMillisecond);
            msi &= 0xFFFFFFFF00000000;
        }

        /// <summary>
        /// This will be called on 5 minute intervals to ensure that we catch
        /// tickcount rollovers and increment the msi field appropriately.
        /// </summary>
        /// <param name="state">Not used.</param>
        private static void OnTimer(object state)
        {
            GetTime();  // This forces a counter wraparound check
        }

        /// <summary>
        /// Returns the current time relative to time the system started.
        /// </summary>
        private static DateTime GetTime()
        {
            uint curCount;

            lock (syncLock)
            {
                curCount = (uint)Environment.TickCount;

                if (curCount < lastCount)
                {
                    // Environment.TickCount must have wrapped around since the last time
                    // SysTime.Now was called.

                    msi += (uint)0x8000000;
                }

                lastCount = curCount;
            }

            return new DateTime((long)((ulong)TimeSpan.TicksPerMillisecond * (msi | (ulong)(uint)curCount)));
        }

        /// <summary>
        /// Returns the current time relative to time the system started.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if there's a problem with the system timer.</exception>
        public static DateTime Now
        {
            get
            {
                if (resolution == TimeSpan.Zero)
                {
                    throw new InvalidOperationException("System timer could not be initialized.");
                }

                return GetTime();
            }
        }

        /// <summary>
        /// Returns the resolution of the underlying system timer.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if there's a problem with the system timer.</exception>
        public static TimeSpan Resolution
        {
            get
            {
                if (resolution == TimeSpan.Zero)
                {
                    throw new InvalidOperationException("System timer could not be initialized.");
                }

                return resolution;
            }
        }

        /// <summary>
        /// Returns what is essentially an infinite timespan.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value returned will calculated such that when added to the
        /// current <see cref="SysTime" />.<see cref="Now" /> value that the
        /// result will be <see cref="DateTime.MaxValue" /> minus one year.
        /// </para>
        /// <para>
        /// This is useful for situations where you need specify an infinite
        /// timeout but you want to avoid wrap-around when adding this to
        /// the current <see cref="SysTime" />.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if there's a problem with the system timer.</exception>
        public static TimeSpan Infinite
        {
            get { return DateTime.MaxValue - SysTime.Now - year; }
        }

        /// <summary>
        /// Tracks an external time source.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="SysTime" /> class is also capable of maintaining rough
        /// synchronization with an external time source.  To use this feature,
        /// you'll periodically get the time from the external source and assign
        /// it to the static <see cref="ExternalNow" /> property then you can
        /// use the <see cref="ExternalNow" /> property to get and estimate of the
        /// current external time.
        /// </para>
        /// <para>
        /// The local side clock will likely drift over time, resulting in a skew
        /// between the time returned by <see cref="ExternalNow" /> and the actual
        /// time at the external source.  This skew can be limited by getting the
        /// external time and assigning it to <see cref="ExternalNow" /> more
        /// frequently.
        /// </para>
        /// <note>
        /// The time returned by <see cref="ExternalNow" /> is not guaranteed to be
        /// monotimically increasing since reported times may jump around as the
        /// bias between the local and external clocks are adjusted.
        /// </note>
        /// </remarks>
        public static DateTime ExternalNow
        {
            get { return DateTime.UtcNow + externalBias; }
            set { externalBias = ExternalNow - DateTime.UtcNow; }
        }
    }
}
