//-----------------------------------------------------------------------------
// FILE:	    CronSchedule.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cadence.Internal;

namespace Neon.Cadence
{
    /// <summary>
    /// Describes a CRON workflow schedule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All of the schedule properties are optional and at least one must be non <c>null</c>
    /// for the workflow to be scheduled as a recurring CRON job.
    /// </para>
    /// <para>
    /// The properties can be combined in various ways to specify the schedule.  Here
    /// are some examples:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="Minute"/><c>=15</c></term>
    ///     <description>
    ///     Workflow will start 15 minutes after the top of the hour (for every hour of every day).
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Minute"/><c>=15</c>, <see cref="Hour"/><c>=8</c></term>
    ///     <description>
    ///     Workflow will start at <b>8:15am</b> every day.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Hour"/><c>=8</c>, <see cref="DayOfWeek.Friday"/></term>
    ///     <description>
    ///     The workflow will start at <b>8am</b> every Friday morning.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Month"/><c>4</c>, <see cref="DayOfMonth"/><c>15</c></term>
    ///     <description>
    ///     The workflow will start at <b>12am</b> every <b>April 15th</b>.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public struct CronSchedule
    {
        /// <summary>
        /// Optionally specifies the minute of the hour when the workflow will be started
        /// <b>(0...59)</b>.
        /// </summary>
        public int? Minute { get; set; }

        /// <summary>
        /// Optionally specifies the hour of the day when the workflow will be started
        /// <b>(0...23)</b>.
        /// </summary>
        public int? Hour { get; set; }

        /// <summary>
        /// Optionally specifies the hour of the day when the workflow will be started
        /// <b>(1...31)</b>.
        /// </summary>
        public int? DayOfMonth { get; set; }

        /// <summary>
        /// Optionally specifies the month of the year when the workflow is started
        /// <b>(1...12)</b>.
        /// </summary>
        public int? Month { get; set; }

        /// <summary>
        /// Optionally specifies the day of the week when the workflow is started.
        /// </summary>
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>
        /// Converts the class into the corresponding internal Cadence string representation.
        /// </summary>
        /// <returns>The schedule string or <c>null</c>.</returns>
        /// <exception cref="ArgumentException">Thrown if any of the schedule properties are invalid.</exception>
        internal string ToInternal()
        {
            if (Minute == null && Hour == null && DayOfMonth == null && Month == null && DayOfWeek == null)
            {
                return null;
            }

            var minute     = string.Empty;
            var hour       = string.Empty;
            var dayOfMonth = string.Empty;
            var month      = string.Empty;
            var dayOfWeek  = string.Empty;

            if (Minute != null)
            {
                if (Minute.Value < 0 || Minute.Value > 59)
                {
                    throw new ArgumentException($"[{nameof(CronSchedule)}.{nameof(Minute)}={Minute}] is invalid.");
                }

                minute = Minute.ToString();
            }

            if (Hour != null)
            {
                if (Hour.Value < 0 || Hour.Value > 23)
                {
                    throw new ArgumentException($"[{nameof(CronSchedule)}.{nameof(Hour)}={Hour}] is invalid.");
                }

                hour = Hour.ToString();
            }

            if (DayOfMonth != null)
            {
                if (DayOfMonth.Value < 1 || DayOfMonth.Value > 31)
                {
                    throw new ArgumentException($"[{nameof(CronSchedule)}.{nameof(DayOfMonth)}={DayOfMonth}] is invalid.");
                }

                dayOfMonth = DayOfMonth.ToString();
            }

            if (Month != null)
            {
                if (Month.Value < 1 || Month.Value > 12)
                {
                    throw new ArgumentException($"[{nameof(CronSchedule)}.{nameof(Month)}={Month}] is invalid.");
                }

                month = Month.ToString();
            }

            if (DayOfWeek != null)
            {
                dayOfMonth = ((int)DayOfWeek).ToString();
            }

            return $"{minute} {hour} {dayOfMonth} {month} {dayOfWeek}";
        }
    }
}
