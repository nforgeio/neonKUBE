//-----------------------------------------------------------------------------
// FILE:	    RandomExtensions.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    /// <summary>
    /// <see cref="Random"/> class extension methods.
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>
        /// Returns a random index into a sequence whose length is specified.
        /// </summary>
        /// <param name="random">The <see cref="Random"/> instance.</param>
        /// <param name="length">The sequence length.</param>
        /// <returns>The random index.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if length is &lt;= 0.</exception>
        public static int NextIndex(this Random random, int length)
        {
            if (length <= 0)
            {
                throw new IndexOutOfRangeException();
            }

            return random.Next() % length;
        }

        /// <summary>
        /// Returns a random <see cref="TimeSpan"/> between zero and a specified maximum.
        /// </summary>
        /// <param name="random">The <see cref="Random"/> instance.</param>
        /// <param name="maxInterval">The maximum interval.</param>
        /// <returns>The random timespan.</returns>
        /// <remarks>
        /// This method is useful for situations where its desirable to have some variation
        /// in a delay before performing an activity like retrying an operation or performing
        /// a background task.
        /// </remarks>
        public static TimeSpan RandomTimespan(this Random random, TimeSpan maxInterval)
        {
            Covenant.Requires<ArgumentException>(maxInterval >= TimeSpan.Zero);

            return TimeSpan.FromSeconds(maxInterval.TotalSeconds * random.NextDouble());
        }

        /// <summary>
        /// Returns a <see cref="TimeSpan"/> between the specified base interval
        /// plus a random period of the specified fraction of the value.
        /// </summary>
        /// <param name="random">The <see cref="Random"/> instance.</param>
        /// <param name="baseInterval">The base interval.</param>
        /// <param name="fraction">The fractional multiplier for the random component.</param>
        /// <returns>The random timespan.</returns>
        /// <remarks>
        /// <para>
        /// The value returned is at least as large as <paramref name="baseInterval" /> with an
        /// added random fractional interval if <paramref name="fraction" /> is positive or the value
        /// returned may be less than <paramref name="baseInterval" /> for a negative <paramref name="fraction" />.  
        /// This is computed via:
        /// </para>
        /// <code language="cs">
        /// baseInterval + RandTimespan(TimeSpan.FromSeconds(baseInterval.TotalSeconds * fraction));
        /// </code>
        /// <para>
        /// This method is useful for situations where its desirable to have some variation
        /// in a delay before performing an activity like retrying an operation or performing
        /// a background task.
        /// </para>
        /// </remarks>
        public static TimeSpan RandomTimespan(this Random random, TimeSpan baseInterval, double fraction)
        {
            Covenant.Requires<ArgumentException>(baseInterval >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(fraction >= 0.0);

            if (fraction == 0.0)
            {
                return baseInterval;
            }

            return baseInterval + RandomTimespan(random, TimeSpan.FromSeconds(baseInterval.TotalSeconds * fraction));
        }
    }
}
