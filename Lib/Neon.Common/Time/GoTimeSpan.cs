//-----------------------------------------------------------------------------
// FILE:	    GoTimeSpan.cs
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
using System.Globalization;
using System.Diagnostics.Contracts;
using System.Threading;

namespace Neon.Time
{
    /// <summary>
    /// Implements support for GO Language formatted durations.  This class is
    /// useful for integrating with GO applications.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <see cref="GoTimeSpan"/> measures time down 1 nanosecond resolution whereas
    /// <see cref="TimeSpan"/>'s resolution is 100ns and both implementations use
    /// a signed 64-bit integer as the underlying representation.  This means that
    /// <see cref="GoTimeSpan"/> can represent of maximum duration of about 290
    /// years (positive and negative) where <see cref="TimeSpan"/> can handle 
    /// about 29,000 years.
    /// </para>
    /// <para>
    /// This class will throw a <see cref="ArgumentOutOfRangeException"/> when converting
    /// a <see cref="TimeSpan"/> that is beyound the capability of a <see cref="GoTimeSpan"/>.
    /// </para>
    /// </note>
    /// </remarks>
    public struct GoTimeSpan
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The number of nanosecond ticks per micrososecond.
        /// </summary>
        public const long TicksPerMicrosecond = 1000;

        /// <summary>
        /// The number of nanosecond ticks per millisecond.
        /// </summary>
        public const long TicksPerMillisecond = TicksPerMicrosecond * 1000;

        /// <summary>
        /// The number of nanosecond ticks per second;
        /// </summary>
        public const long TicksPerSecond = TicksPerMillisecond * 1000;

        /// <summary>
        /// The number of nanosecond ticks per minute.
        /// </summary>
        public const long TicksPerMinute = TicksPerSecond * 60;

        /// <summary>
        /// The number of nanosecond ticks per minute.
        /// </summary>
        public const long TicksPerHour = TicksPerMinute * 60;

        /// <summary>
        /// Returns a zero <see cref="GoTimeSpan"/> .
        /// </summary>
        public static GoTimeSpan Zero { get; private set; } = GoTimeSpan.FromNanoseconds(0);

        /// <summary>
        /// Returns the minimum possible <see cref="GoTimeSpan"/>.
        /// </summary>
        public static GoTimeSpan MinValue { get; private set; } = GoTimeSpan.FromNanoseconds(long.MinValue);

        /// <summary>
        /// Returns the maximum possible <see cref="GoTimeSpan"/>.
        /// </summary>
        public static GoTimeSpan MaxValue { get; private set; } = GoTimeSpan.FromNanoseconds(long.MaxValue);

        /// <summary>
        /// The minimum value serialized to a string (computed by hand to avoid 64-bit wrap around issues.
        /// </summary>
        private const string MinValueString = "-2562047h47m16s854ms775us808ns";

        /// <summary>
        /// Implicitly converts a <see cref="GoTimeSpan"/> into a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="goTimeSpan">The input <see cref="GoTimeSpan"/>.</param>
        /// <returns>The equivalent <see cref="TimeSpan"/>.</returns>
        public static implicit operator TimeSpan(GoTimeSpan goTimeSpan)
        {
            return goTimeSpan.TimeSpan;
        }

        /// <summary>
        /// Implicitly converts a <see cref="TimeSpan"/> into a <see cref="GoTimeSpan"/>.
        /// </summary>
        /// <param name="timespan">The input <see cref="TimeSpan"/>.</param>
        /// <returns>The equivalent <see cref="GoTimeSpan"/>.</returns>
        public static implicit operator GoTimeSpan(TimeSpan timespan)
        {
            return new GoTimeSpan(timespan);
        }

        /// <summary>
        /// Attempts to parse a GO formatted timespan.  
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="goTimeSpan">Returns as the parsed timespan on success.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <remarks>
        /// <para>
        /// The input is a possibly signed sequence of decimal numbers, each with 
        /// optional fraction and a unit suffix, such as "300ms", "-1.5h" or 
        /// "2h45m". Valid time units are "ns", "us" (or "µs"), "ms", "s", "m", "h". 
        /// </para>
        /// <note>
        /// GO timespans are limited to about 290 years (the maximum number of
        /// nanoseconds that can be represented in a signed 64-bit integer).
        /// </note>
        /// </remarks>
        public static bool TryParse(string input, out GoTimeSpan goTimeSpan)
        {
            goTimeSpan = new GoTimeSpan();

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            input = input.Trim().ToLowerInvariant();

            switch (input)
            {
                case "0":

                    // Special case 0 with no unit suffix.

                    return true;

                case MinValueString:

                    goTimeSpan = MinValue;
                    return true;
            }

            double  dTicks     = 0.0;
            long    ticks      = 0;
            bool    isNegative = false;
            int     pStart, pEnd;

            pStart = 0;

            if (input[pStart] == '+')
            {
                pStart++;
            }
            else if (input[pStart] == '-')
            {
                pStart++;
                isNegative = true;
            }

            if (pStart >= input.Length)
            {
                return false;
            }

            var unitChars = new char[] { 'h', 'm', 's', 'u', 'µ', 'n' };

            while (true)
            {
                pEnd = input.IndexOfAny(unitChars, pStart);

                if (pEnd == -1 || pStart == pEnd)
                {
                    return false;
                }

                if (!double.TryParse(input.Substring(pStart, pEnd - pStart), NumberStyles.AllowDecimalPoint, NumberFormatInfo.InvariantInfo, out var value))
                {
                    return false;
                }

                switch (input[pEnd])
                {
                    case 'h':

                        ticks  += (long)(value * TicksPerHour);
                        dTicks += value * TicksPerHour;
                        break;

                    case 'm':

                        if (pEnd + 1 >= input.Length || input[pEnd + 1] != 's')
                        {
                            ticks  += (long)(value * TicksPerMinute);
                            dTicks += value * TicksPerMinute;
                        }
                        else
                        {
                            ticks  += (long)(value * TicksPerMillisecond);
                            dTicks += value * TicksPerMillisecond;
                            pEnd++;
                        }
                        break;

                    case 's':

                        ticks  += (long)(value * TicksPerSecond);
                        dTicks += value * TicksPerSecond;
                        break;

                    case 'µ':
                    case 'u':

                        if (pEnd + 1 >= input.Length || input[pEnd + 1] != 's')
                        {
                            return false;
                        }

                        ticks  += (long)(value * TicksPerMicrosecond);
                        dTicks += value * TicksPerMicrosecond;
                        pEnd++;
                        break;

                    case 'n':

                        if (pEnd + 1 >= input.Length || input[pEnd + 1] != 's')
                        {
                            return false;
                        }

                        ticks  += (long)value;
                        dTicks += value;
                        pEnd++;
                        break;

                    default:

                        return false;
                }

                pStart = pEnd + 1;

                if (pStart >= input.Length)
                {
                    if (dTicks > long.MaxValue)
                    {
                        return false;   // Value is out of range 
                    }

                    // We've finished parsing the input.

                    if (isNegative)
                    {
                        goTimeSpan.Ticks = -ticks;
                    }
                    else
                    {
                        goTimeSpan.Ticks = ticks;
                    }

                    return true;
                }
            }
        }

        /// <summary>
        /// Parses a <see cref="GoTimeSpan"/> from a string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The parsed <see cref="GoTimeSpan"/>.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        public static GoTimeSpan Parse(string input)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input));

            if (!TryParse(input, out var goTimeSpan))
            {
                throw new FormatException($"Cannot parse [{nameof(GoTimeSpan)}] string: [{input}]");
            }

            return goTimeSpan;
        }

        /// <summary>
        /// Creates a <see cref="GoTimeSpan"/> from a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="timespan">The input time span.</param>
        /// <returns>The new <see cref="GoTimeSpan"/>.</returns>
        public static GoTimeSpan FromTimeSpan(TimeSpan timespan)
        {
            return new GoTimeSpan(timespan);
        }

        /// <summary>
        /// Returns a <see cref="GoTimeSpan"/> from nanoseconds.
        /// </summary>
        /// <param name="nanoseconds">The duration in nanoseconds.</param>
        /// <returns>The new <see cref="GoTimeSpan"/>.</returns>
        public static GoTimeSpan FromNanoseconds(long nanoseconds)
        {
            return new GoTimeSpan(nanoseconds);
        }

        /// <summary>
        /// Returns a <see cref="GoTimeSpan"/> from microseconds.
        /// </summary>
        /// <param name="milliseconds">The duration in microseconds.</param>
        /// <returns>The new <see cref="GoTimeSpan"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoTimeSpan"/>.</exception>
        public static GoTimeSpan FromMicroseconds(double milliseconds)
        {
            return new GoTimeSpan(ToTicks(milliseconds * TicksPerMicrosecond));
        }

        /// <summary>
        /// Returns a <see cref="GoTimeSpan"/> from milliseconds.
        /// </summary>
        /// <param name="milliseconds">The duration in milliseconds.</param>
        /// <returns>The new <see cref="GoTimeSpan"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoTimeSpan"/>.</exception>
        public static GoTimeSpan FromMilliseconds(double milliseconds)
        {
            return new GoTimeSpan(ToTicks(milliseconds * TicksPerMillisecond));
        }

        /// <summary>
        /// Returns a <see cref="GoTimeSpan"/> from seconds.
        /// </summary>
        /// <param name="seconds">The duration in seconds.</param>
        /// <returns>The new <see cref="GoTimeSpan"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoTimeSpan"/>.</exception>
        public static GoTimeSpan FromSeconds(double seconds)
        {
            return new GoTimeSpan(ToTicks(seconds * TicksPerSecond));
        }

        /// <summary>
        /// Returns a <see cref="GoTimeSpan"/> from minutes.
        /// </summary>
        /// <param name="minutes">The duration in minutes.</param>
        /// <returns>The new <see cref="GoTimeSpan"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoTimeSpan"/>.</exception>
        public static GoTimeSpan FromMinutes(double minutes)
        {
            return new GoTimeSpan(ToTicks(minutes * TicksPerMinute));
        }

        /// <summary>
        /// Returns a <see cref="GoTimeSpan"/> from hours.
        /// </summary>
        /// <param name="hours">The duration in hours.</param>
        /// <returns>The new <see cref="GoTimeSpan"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoTimeSpan"/>.</exception>
        public static GoTimeSpan FromHours(double hours)
        {
            return new GoTimeSpan(ToTicks(hours * TicksPerHour));
        }

        /// <summary>
        /// Converts a <c>double</c> nanosecond count to a <c>long</c>, ensuring that the
        /// result can be represented as a <see cref="GoTimeSpan"/>.
        /// </summary>
        /// <param name="nanoseconds">The input <c>douuble</c>.</param>
        /// <returns>The output <c>long</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoTimeSpan"/>.</exception>
        private static long ToTicks(double nanoseconds)
        {
            if (long.MinValue <= nanoseconds && nanoseconds <= long.MaxValue)
            {
                return (long)nanoseconds;
            }

            throw new ArgumentOutOfRangeException($"Value is outside the range of a [{nameof(GoTimeSpan)}].");
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs a <see cref="GoTimeSpan"/> from nanoseconds.
        /// </summary>
        /// <param name="nanoseconds">The duration in nanoseconds.</param>
        public GoTimeSpan(long nanoseconds)
        {
            this.Ticks = nanoseconds;
        }

        /// <summary>
        /// Constructs an instance from a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="timespan">The time span.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoTimeSpan"/>.</exception>
        public GoTimeSpan(TimeSpan timespan)
        {
            this.Ticks = ToTicks(timespan.Ticks * 100.0);
        }

        /// <summary>
        /// The duration expressed as nanosecond ticks.
        /// </summary>
        public long Ticks { get; private set; }

        /// <summary>
        /// Returns the duration as nanoseconds.
        /// </summary>
        public double TotalNanoseconds
        {
            get { return Ticks; }
        }

        /// <summary>
        /// Returns the total number of microseconds.
        /// </summary>
        public double TotalMicroseconds
        {
            get { return Ticks / (double)TicksPerMicrosecond; }
        }

        /// <summary>
        /// Returns the total number of milliseconds.
        /// </summary>
        public double TotalMilliseconds
        {
            get { return Ticks / (double)TicksPerMillisecond; }
        }

        /// <summary>
        /// Returns the total number of seconds.
        /// </summary>
        public double TotalSeconds
        {
            get { return Ticks / (double)TicksPerSecond; }
        }

        /// <summary>
        /// Returns the total number of minutes.
        /// </summary>
        public double TotalMinutes
        {
            get { return Ticks / (double)TicksPerMinute; }
        }

        /// <summary>
        /// Returns the total number of hours.
        /// </summary>
        public double TotalHours
        {
            get { return Ticks / (double)TicksPerHour; }
        }

        /// <summary>
        /// Returns the nanosecond component of the duration.
        /// </summary>
        public long Nanoseconds
        {
            get { return Ticks % 1000; }
        }

        /// <summary>
        /// Returns the microsecond component of the duration.
        /// </summary>
        public long Microseconds
        {
            get { return (Ticks % (TicksPerMicrosecond * 1000)) / TicksPerMicrosecond; }
        }

        /// <summary>
        /// Returns the millisecond component of the duration.
        /// </summary>
        public long Milliseconds
        {
            get { return (Ticks % (TicksPerMillisecond * 1000)) / TicksPerMillisecond; }
        }

        /// <summary>
        /// Returns the second component of the duration.
        /// </summary>
        public long Seconds
        {
            get { return (Ticks - Hours * TicksPerHour - Minutes * TicksPerMinute) / TicksPerSecond; }
        }

        /// <summary>
        /// Returns the minutes component of the duration.
        /// </summary>
        public long Minutes
        {
            get { return (Ticks - Hours * TicksPerHour) / TicksPerMinute; }
        }

        /// <summary>
        /// Returns the hours component of the duration.
        /// </summary>
        public long Hours
        {
            get { return Ticks / TicksPerHour; }
        }

        /// <summary>
        /// Returns the equivalent <see cref="TimeSpan"/>.
        /// </summary>
        public TimeSpan TimeSpan
        {
            get { return TimeSpan.FromTicks(Ticks / 100); }
        }

        /// <summary>
        /// Renders the time span as a GO Duration compatible string.
        /// </summary>
        /// <returns>The GO duration.</returns>
        public override string ToString()
        {
            if (Ticks == 0)
            {
                return "0";
            }
            else if (Ticks == long.MinValue)
            {
                // Special case the minimum negative duration so we negate Ticks
                // below without worrying about 64-bit wrap around.  I computed this
                // by hand.

                return MinValueString;
            }

            string      output = string.Empty;
            GoTimeSpan  absolute;

            if (Ticks < 0)
            {
                output += "-";
                absolute = FromNanoseconds(-this.Ticks);
            }
            else
            {
                absolute = FromNanoseconds(this.Ticks);
            }

            if (absolute.Hours > 0)
            {
                output += $"{absolute.Hours}h";
            }

            if (absolute.Minutes > 0)
            {
                output += $"{absolute.Minutes}m";
            }

            if (absolute.Seconds > 0)
            {
                output += $"{absolute.Seconds}s";
            }

            if (absolute.Milliseconds > 0)
            {
                output += $"{absolute.Milliseconds}ms";
            }

            if (absolute.Microseconds > 0)
            {
                output += $"{absolute.Microseconds}us";
            }

            if (absolute.Nanoseconds > 0)
            {
                output += $"{absolute.Nanoseconds}ns";
            }

            return output;
        }
    }
}
