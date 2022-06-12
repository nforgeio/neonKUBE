//-----------------------------------------------------------------------------
// FILE:	    DateTimeExtensions.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    /// <summary>
    /// <see cref="DateTime"/> extensions.
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Rounds a <see cref="DateTime"/> up to the nearest specified interval.
        /// 
        /// var date = new DateTime(2010, 02, 05, 10, 35, 25, 450); // 2010/02/05 10:35:25
        /// var roundedUp = date.RoundUp(TimeSpan.FromMinutes(15)); // 2010/02/05 10:45:00
        /// </summary>
        /// <param name="dt">The datetime to be rounded.</param>
        /// <param name="interval">The time interval to be rounded to.</param>
        /// <returns></returns>
        public static DateTime RoundUp(this DateTime dt, TimeSpan interval)
        {
            Covenant.Requires<ArgumentException>(interval > TimeSpan.Zero, nameof(interval));

            var modTicks = dt.Ticks % interval.Ticks;
            var delta = modTicks != 0 ? interval.Ticks - modTicks : 0;
            return new DateTime(dt.Ticks + delta, dt.Kind);
        }

        /// <summary>
        /// Rounds a <see cref="DateTime"/> down to the nearest specified interval.
        /// 
        /// var date = new DateTime(2010, 02, 05, 10, 35, 25, 450);     // 2010/02/05 10:35:25
        /// var roundedDown = date.RoundDown(TimeSpan.FromMinutes(15)); // 2010/02/05 10:30:00
        /// </summary>
        /// <param name="dt">The datetime to be rounded.</param>
        /// <param name="interval">The time interval to be rounded to.</param>
        /// <returns></returns>
        public static DateTime RoundDown(this DateTime dt, TimeSpan interval)
        {
            Covenant.Requires<ArgumentException>(interval > TimeSpan.Zero, nameof(interval));

            var delta = dt.Ticks % interval.Ticks;
            return new DateTime(dt.Ticks - delta, dt.Kind);
        }

        /// <summary>
        /// Rounds a <see cref="DateTime"/> to the nearest specified interval.
        /// 
        /// var date = new DateTime(2010, 02, 05, 10, 35, 25, 450);               // 2010/02/05 10:35:25
        /// var roundedToNearest = date.RoundToNearest(TimeSpan.FromMinutes(15)); // 2010/02/05 10:30:00
        /// </summary>
        /// <param name="dt">The datetime to be rounded.</param>
        /// <param name="interval">The time interval to be rounded to.</param>
        /// <returns></returns>
        public static DateTime RoundToNearest(this DateTime dt, TimeSpan interval)
        {
            Covenant.Requires<ArgumentException>(interval > TimeSpan.Zero, nameof(interval));

            var delta    = dt.Ticks % interval.Ticks;
            bool roundUp = delta >= interval.Ticks / 2;
            var offset   = roundUp ? interval.Ticks : 0;

            return new DateTime(dt.Ticks + offset - delta, dt.Kind);
        }
    }
}
