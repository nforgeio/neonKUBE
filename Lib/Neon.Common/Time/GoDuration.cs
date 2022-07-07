//-----------------------------------------------------------------------------
// FILE:	    GoDuration.cs
// CONTRIBUTOR: Jeff Lill
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
    /// <see cref="GoDuration"/> measures time down 1 nanosecond resolution whereas
    /// <see cref="TimeSpan"/>'s resolution is 100ns and both implementations use
    /// a signed 64-bit integer as the underlying representation.  This means that
    /// <see cref="GoDuration"/> can represent of maximum duration of about 290
    /// years (positive and negative) where <see cref="TimeSpan"/> can handle 
    /// about 29,000 years.
    /// </para>
    /// <para>
    /// This class will throw a <see cref="ArgumentOutOfRangeException"/> when converting
    /// a <see cref="TimeSpan"/> that is beyound the capability of a <see cref="GoDuration"/>.
    /// </para>
    /// </note>
    /// </remarks>
    public struct GoDuration
    {
        //---------------------------------------------------------------------
        // Static members

        private const string hourRegEx         = @"(\d+(\.\d+)?h)";
        private const string minuteRegEx       = @"(\d+(\.\d+)?m)";
        private const string secondRegEx       = @"(\d+(\.\d+)?s)";
        private const string millisecondRegEx  = @"(\d+(\.\d+)?ms)";
        private const string microsecondRegEx  = @"((\d+(\.\d+)?us)|(\d+(.\d+)?µs))";
        private const string nanosecondRegex   = @"(\d+(\.\d+)?ns)";
        private const string combinationsRegEx = $@"({hourRegEx}{minuteRegEx}?{secondRegEx}?{millisecondRegEx}?{microsecondRegEx}?{nanosecondRegex}?)|" +
                                                 $@"({hourRegEx}?{minuteRegEx}{secondRegEx}?{millisecondRegEx}?{microsecondRegEx}?{nanosecondRegex}?)|" +
                                                 $@"({hourRegEx}?{minuteRegEx}?{secondRegEx}{millisecondRegEx}?{microsecondRegEx}?{nanosecondRegex}?)|" +
                                                 $@"({hourRegEx}?{minuteRegEx}?{secondRegEx}?{millisecondRegEx}{microsecondRegEx}?{nanosecondRegex}?)|" +
                                                 $@"({hourRegEx}?{minuteRegEx}?{secondRegEx}?{millisecondRegEx}?{microsecondRegEx}{nanosecondRegex}?)|" +
                                                 $@"({hourRegEx}?{minuteRegEx}?{secondRegEx}?{millisecondRegEx}?{microsecondRegEx}?{nanosecondRegex})";

        /// <summary>
        /// The partial regular expression that can be used to validate GOLANG duration strings.  This
        /// does not include the start/end anchors and is suitable for situations where these are implied.
        /// </summary>
        public const string PartialRegEx = $@"0|({combinationsRegEx})";

        /// <summary>
        /// The full regular expression (including start/end anchors) use to validate GOLANG duration strings.
        /// </summary>
        public const string RegEx = $"^({PartialRegEx})$";

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
        /// Returns a zero <see cref="GoDuration"/> .
        /// </summary>
        public static GoDuration Zero { get; private set; } = GoDuration.FromNanoseconds(0);

        /// <summary>
        /// Returns the minimum possible <see cref="GoDuration"/>.
        /// </summary>
        public static GoDuration MinValue { get; private set; } = GoDuration.FromNanoseconds(long.MinValue);

        /// <summary>
        /// Returns the maximum possible <see cref="GoDuration"/>.
        /// </summary>
        public static GoDuration MaxValue { get; private set; } = GoDuration.FromNanoseconds(long.MaxValue);

        /// <summary>
        /// The minimum value serialized to a string (computed by hand to avoid 64-bit wrap around issues.
        /// </summary>
        private const string MinValueString = "-2562047h47m16s854ms775us808ns";

        /// <summary>
        /// Implicitly converts a <see cref="GoDuration"/> into a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="goTimeSpan">The input <see cref="GoDuration"/>.</param>
        /// <returns>The equivalent <see cref="TimeSpan"/>.</returns>
        public static implicit operator TimeSpan(GoDuration goTimeSpan)
        {
            return goTimeSpan.TimeSpan;
        }

        /// <summary>
        /// Implicitly converts a <see cref="TimeSpan"/> into a <see cref="GoDuration"/>.
        /// </summary>
        /// <param name="timespan">The input <see cref="TimeSpan"/>.</param>
        /// <returns>The equivalent <see cref="GoDuration"/>.</returns>
        public static implicit operator GoDuration(TimeSpan timespan)
        {
            return new GoDuration(timespan);
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
        public static bool TryParse(string input, out GoDuration goTimeSpan)
        {
            goTimeSpan = new GoDuration();

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
        /// Parses a <see cref="GoDuration"/> from a string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The parsed <see cref="GoDuration"/>.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        /// <remarks>
        /// <para>
        /// The input is a possibly signed sequence of decimal numbers, each with 
        /// optional fraction and a unit suffix, such as "300ms", "-1.5h" or 
        /// "2h45m". Valid time units are "ns", "us" (or "µs"), "ms", "s", "m", "h". 
        /// </para>
        /// <note>
        /// <c>null</c> or empty strings are parsed as <see cref="TimeSpan.Zero"/>.
        /// </note>
        /// <note>
        /// GO timespans are limited to about 290 years (the maximum number of
        /// nanoseconds that can be represented in a signed 64-bit integer).
        /// </note>
        /// </remarks>
        public static GoDuration Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return TimeSpan.Zero;
            }

            if (!TryParse(input, out var goTimeSpan))
            {
                throw new FormatException($"Cannot parse [{nameof(GoDuration)}] string: [{input}]");
            }

            return goTimeSpan;
        }

        /// <summary>
        /// Creates a <see cref="GoDuration"/> from a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="timespan">The input time span.</param>
        /// <returns>The new <see cref="GoDuration"/>.</returns>
        public static GoDuration FromTimeSpan(TimeSpan timespan)
        {
            return new GoDuration(timespan);
        }

        /// <summary>
        /// Returns a <see cref="GoDuration"/> from nanoseconds.
        /// </summary>
        /// <param name="nanoseconds">The duration in nanoseconds.</param>
        /// <returns>The new <see cref="GoDuration"/>.</returns>
        public static GoDuration FromNanoseconds(long nanoseconds)
        {
            return new GoDuration(nanoseconds);
        }

        /// <summary>
        /// Returns a <see cref="GoDuration"/> from microseconds.
        /// </summary>
        /// <param name="milliseconds">The duration in microseconds.</param>
        /// <returns>The new <see cref="GoDuration"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoDuration"/>.</exception>
        public static GoDuration FromMicroseconds(double milliseconds)
        {
            return new GoDuration(ToTicks((decimal) milliseconds * TicksPerMicrosecond));
        }

        /// <summary>
        /// Returns a <see cref="GoDuration"/> from milliseconds.
        /// </summary>
        /// <param name="milliseconds">The duration in milliseconds.</param>
        /// <returns>The new <see cref="GoDuration"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoDuration"/>.</exception>
        public static GoDuration FromMilliseconds(double milliseconds)
        {
            return new GoDuration(ToTicks((decimal) milliseconds * TicksPerMillisecond));
        }

        /// <summary>
        /// Returns a <see cref="GoDuration"/> from seconds.
        /// </summary>
        /// <param name="seconds">The duration in seconds.</param>
        /// <returns>The new <see cref="GoDuration"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoDuration"/>.</exception>
        public static GoDuration FromSeconds(double seconds)
        {
            return new GoDuration(ToTicks((decimal) seconds * TicksPerSecond));
        }

        /// <summary>
        /// Returns a <see cref="GoDuration"/> from minutes.
        /// </summary>
        /// <param name="minutes">The duration in minutes.</param>
        /// <returns>The new <see cref="GoDuration"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoDuration"/>.</exception>
        public static GoDuration FromMinutes(double minutes)
        {
            return new GoDuration(ToTicks((decimal) minutes * TicksPerMinute));
        }

        /// <summary>
        /// Returns a <see cref="GoDuration"/> from hours.
        /// </summary>
        /// <param name="hours">The duration in hours.</param>
        /// <returns>The new <see cref="GoDuration"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoDuration"/>.</exception>
        public static GoDuration FromHours(double hours)
        {
            return new GoDuration(ToTicks((decimal) hours * TicksPerHour));
        }

        /// <summary>
        /// Converts a <c>double</c> nanosecond count to a <c>long</c>, ensuring that the
        /// result can be represented as a <see cref="GoDuration"/>.
        /// </summary>
        /// <param name="nanoseconds">The input <c>douuble</c>.</param>
        /// <returns>The output <c>long</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoDuration"/>.</exception>
        private static long ToTicks(decimal nanoseconds)
        {
            if (long.MinValue <= nanoseconds && nanoseconds <= long.MaxValue)
            {
                return (long)nanoseconds;
            }

            throw new ArgumentOutOfRangeException($"Value is outside the range of a [{nameof(GoDuration)}].");
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs a <see cref="GoDuration"/> from nanoseconds.
        /// </summary>
        /// <param name="nanoseconds">The duration in nanoseconds.</param>
        public GoDuration(long nanoseconds)
        {
            this.Ticks = nanoseconds;
        }

        /// <summary>
        /// Constructs an instance from a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="timespan">The time span.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the input is outside the capability of a <see cref="GoDuration"/>.</exception>
        public GoDuration(TimeSpan timespan)
        {
            this.Ticks = ToTicks(timespan.Ticks * 100.0m);
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
                // Special case the minimum negative duration so we can negate [Ticks]
                // below without worrying about 64-bit wrap around.  I computed this
                // by hand.

                return MinValueString;
            }

            string      output = string.Empty;
            GoDuration  absolute;

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

        /// <summary>
        /// Renders the duration into a string including hour, minute, and seconds with
        /// fractions as required, avoiding millisecond, microsecond, and nanosecond units.
        /// </summary>
        /// <returns>The pretty string.</returns>
        public string ToPretty()
        {
            if (Ticks == 0)
            {
                return "0";
            }
            else if (Ticks == long.MinValue)
            {
                // Special case the minimum negative duration so we can negate [Ticks]
                // below without worrying about 64-bit wrap around.  I computed this
                // by hand.

                return MinValueString;  // $hack(jefflill): I'm not going to worry about malking this pretty.
            }

            string      output = string.Empty;
            GoDuration  absolute;

            if (Ticks < 0)
            {
                output  += "-";
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

            var hoursAndMinutes = TimeSpan.FromHours(absolute.Hours) + TimeSpan.FromMinutes(absolute.Minutes);
            var seconds         = ((TimeSpan)absolute - hoursAndMinutes).TotalSeconds;

            if (seconds > 0)
            {
                output += $"{seconds}s";
            }

            return output;
        }
    }
}
