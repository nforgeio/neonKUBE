//-----------------------------------------------------------------------------
// FILE:        TimeOfDay.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace Neon.Time
{
    /// <summary>
    /// Represents the time offset since the beginning of the day.
    /// </summary>
    /// <threadsafety instance="true" />
    public struct TimeOfDay
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Attempts to parse a string of the form HH:MM or HH:MM:SS into a
        /// time of day offset.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <param name="timeOfDay">Returns as the parsed time of day on success.</param>
        /// <returns><c>true</c> if the string was parsed successfully, <c>false</c> otherwise.</returns>
        public static bool TryParse(string value, out TimeOfDay timeOfDay)
        {
            string[]    fields;
            int         hours;
            int         minutes;
            int         seconds;
            TimeSpan    offset;

            timeOfDay = default;

            if (value == null)
            {
                return false;
            }

            fields = value.Split(':');

            switch (fields.Length)
            {
                case 2:

                    // Parsing HH:MM

                    if (!int.TryParse(fields[0].Trim(), out hours))
                    {
                        return false;
                    }

                    if (!int.TryParse(fields[1].Trim(), out minutes))
                    {
                        return false;
                    }

                    seconds = 0;
                    break;

                case 3:

                    // Parsing HH:MM:SS

                    if (!int.TryParse(fields[0].Trim(), out hours))
                    {
                        return false;
                    }

                    if (!int.TryParse(fields[1].Trim(), out minutes))
                    {
                        return false;
                    }

                    if (!int.TryParse(fields[2].Trim(), out seconds))
                    {
                        return false;
                    }
                    break;

                default:

                    return false;
            }

            offset = new TimeSpan(hours, minutes, seconds);

            if (offset < TimeSpan.Zero || offset.TotalHours >= 24)
            {
                return false;
            }

            timeOfDay.offset = offset;
            return true;
        }

        //---------------------------------------------------------------------
        // Instance members

        private TimeSpan offset;

        /// <summary>
        /// Constructs a time of day offset by stripping the date portion
        /// from the parameter passed.
        /// </summary>
        /// <param name="date">The source date time.</param>
        public TimeOfDay(DateTime date)
        {
            offset = date.TimeOfDay;
        }

        /// <summary>
        /// Constructs a time of day offset from hours and minutes.
        /// </summary>
        /// <param name="hours">The hours.</param>
        /// <param name="minutes">The minutes.</param>
        /// <exception cref="ArgumentException">Thrown if the specified offset is negative or &gt;= 24 hours.</exception>
        public TimeOfDay(int hours, int minutes)
        {
            offset = new TimeSpan(hours, minutes, 0);
            Validate();
        }

        /// <summary>
        /// Constructs a time of day offset from hours, minutes, and seconds.
        /// </summary>
        /// <param name="hours">The hours.</param>
        /// <param name="minutes">The minutes.</param>
        /// <param name="seconds">The seconds.</param>
        /// <exception cref="ArgumentException">Thrown if the specified offset is negative or &gt;= 24 hours.</exception>
        public TimeOfDay(int hours, int minutes, int seconds)
        {
            offset = new TimeSpan(hours, minutes, seconds);
            Validate();
        }

        /// <summary>
        /// Constructs a time of day offset by parsing a string of the form HH:MM or HH:MM:SS.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <exception cref="ArgumentException">Thrown if the string passed does not represent a valid time of day offset.</exception>
        public TimeOfDay(string value)
        {
            TimeOfDay parsed;

            if (!TryParse(value, out parsed))
            {
                throw new ArgumentException(string.Format("Error parsing TimeOfDay value [{0}].", value ?? "null"));
            }

            this.offset = parsed.offset;
        }

        /// <summary>
        /// Constructs a time of day offset from a <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="offset">The time offset from the beginning of the day.</param>
        /// <exception cref="ArgumentException">Thrown if the specified offset is negative or &gt;= 24 hours.</exception>
        public TimeOfDay(TimeSpan offset)
        {
            this.offset = offset;
            Validate();
        }

        /// <summary>
        /// Checks to make sure that the specified offset is valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the specified offset is negative or &gt;= 24 hours.</exception>
        private void Validate()
        {
            if (offset < TimeSpan.Zero || offset.TotalHours >= 24)
            {
                throw new ArgumentException("TimeOfDay offset cannot be negative or greater than or equal to 24 hours.", "offset");
            }
        }

        /// <summary>
        /// Returns the hours part of the time offset.
        /// </summary>
        public int Hour
        {
            get { return offset.Hours; }
        }

        /// <summary>
        /// Returns the minutes part of the time offset.
        /// </summary>
        public int Minute
        {
            get { return offset.Minutes; }
        }

        /// <summary>
        /// Returns the seconds part of the time offset.
        /// </summary>
        public int Second
        {
            get { return offset.Seconds; }
        }

        /// <summary>
        /// Returns the offset as a <see cref="TimeSpan" />.
        /// </summary>
        public TimeSpan TimeSpan
        {
            get { return offset; }
        }

        /// <summary>
        /// Renders the time of day value as a string formatted as HH:MM:SS.
        /// </summary>
        /// <returns>The formatted string.</returns>
        public override string ToString()
        {
            return string.Format("{0:0#}:{1:0#}:{2:0#}", offset.Hours, offset.Minutes, offset.Seconds);
        }
    }
}
