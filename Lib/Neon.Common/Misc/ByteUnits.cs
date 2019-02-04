//-----------------------------------------------------------------------------
// FILE:	    ByteUnits.cs
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Diagnostics;

// $todo(jeff.lill):
//
// PB and PiB units aren't working due to flowting point precision issues.
// I'm going to disable this for now.  Perhaps we can address this by using
// [decimal] instead of [double].

namespace Neon.Common
{
    /// <summary>
    /// <para>
    /// Converts a byte count string with optional units into a count.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>KB</b></term>
    ///     <description>1,000</description>
    /// </item>
    /// <item>
    ///     <term><b>KiB</b></term>
    ///     <description>1,024</description>
    /// </item>
    /// <item>
    ///     <term><b>MB</b></term>
    ///     <description>1000000</description>
    /// </item>
    /// <item>
    ///     <term><b>MiB</b></term>
    ///     <description>1,048,576</description>
    /// </item>
    /// <item>
    ///     <term><b>GB</b></term>
    ///     <description>1,000,000,000</description>
    /// </item>
    /// <item>
    ///     <term><b>GiB</b></term>
    ///     <description>1,073,741,824</description>
    /// </item>
    /// <item>
    ///     <term><b>TB</b></term>
    ///     <description>1,000,000,000,000</description>
    /// </item>
    /// <item>
    ///     <term><b>TiB</b></term>
    ///     <description>1,099,511,627,776</description>
    /// </item>
    /// </list>
    /// </summary>
    public static class ByteUnits
    {
        /// <summary>
        /// One KB: 1,000
        /// </summary>
        public const long KiloBytes = 1000;

        /// <summary>
        /// One MB: 1,000,000
        /// </summary>
        public const long MegaBytes = KiloBytes * KiloBytes;

        /// <summary>
        /// One GB: 1,000,000,000
        /// </summary>
        public const long GigaBytes = MegaBytes * KiloBytes;

        /// <summary>
        /// The constant 1,000,000,000
        /// </summary>
        public const long TeraBytes = GigaBytes * KiloBytes;

#if ALLOW_PENTA
        /// <summary>
        /// One PB: 1,000,000,000,000
        /// </summary>
        public const long PentaBytes = TeraBytes * KiloBytes;
#endif

        /// <summary>
        /// One KiB: 1,024 (2^10)
        /// </summary>
        public const long KibiBytes = 1024;

        /// <summary>
        /// One MiB: 1,048,576 (2^20)
        /// </summary>
        public const long MebiBytes = KibiBytes * KibiBytes;

        /// <summary>
        /// One GiB: 1,073,741,824 (2^30)
        /// </summary>
        public const long GibiBytes = MebiBytes * KibiBytes;

        /// <summary>
        /// The constant 1,099,511,627,776 (2^40)
        /// </summary>
        public const long TebiBytes = GibiBytes * KibiBytes;

#if ALLOW_PENTA
        /// <summary>
        /// One PiB: 1,125,899,906,842,624 (2^50)
        /// </summary>
        public const long PebiBytes = TebiBytes * KibiBytes;
#endif

        /// <summary>
        /// Parses a floating point count string that may include one of the optional
        /// unit suffixes described here <see cref="ByteUnits"/>.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="value">Returns as the output value.</param>
        /// <returns><b>true</b> on success</returns>
        public static bool TryParse(string input, out long value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var units     = 1L;
            var unitLabel = string.Empty;

            // Extract the units (if present).

            if (input.Length == 0 || !char.IsDigit(input[0]))
            {
                return false;
            }

            for (int pos = input.Length - 1; pos > 0; pos--)
            {
                if (!char.IsDigit(input[pos]))
                {
                    unitLabel += input[pos];
                }
                else
                {
                    break;
                }
            }

            var temp = string.Empty;

            foreach (var ch in unitLabel.Reverse())
            {
                temp += ch;
            }

            unitLabel = temp.Trim();
            unitLabel = unitLabel.ToUpperInvariant();

            // Map the unit label to a count.

            if (unitLabel.Length > 0)
            {
                switch (unitLabel)
                {
                    case "B":   units = 1;          break;
                    case "K":   units = KiloBytes;  break;
                    case "KB":  units = KiloBytes;  break;
                    case "KIB": units = KibiBytes;  break;
                    case "M":   units = MegaBytes;  break;
                    case "MB":  units = MegaBytes;  break;
                    case "MIB": units = MebiBytes;  break;
                    case "G":   units = GigaBytes;  break;
                    case "GB":  units = GigaBytes;  break;
                    case "GIB": units = GibiBytes;  break;
                    case "T":   units = TeraBytes;  break;
                    case "TB":  units = TeraBytes;  break;
                    case "TIB": units = TebiBytes;  break;
#if ALLOW_PENTA
                    case "P":   units = PentaBytes; break;
                    case "PB":  units = PentaBytes; break;
                    case "PIB": units = PebiBytes;  break;
#endif

                    default:

                        // Unknown units

                        return false;
                }
            }

            if (unitLabel.Length > 0)
            {
                input = input.Substring(0, input.Length - unitLabel.Length);
            }

            if (!double.TryParse(input.Trim(), NumberStyles.Any, NumberFormatInfo.InvariantInfo, out var raw))
            {
                return false;
            }

            value = (long)(raw * units);

            return value >= 0.0;
        }

        /// <summary>
        /// Parses a byte count and returns a <c>long</c>.
        /// </summary>
        /// <param name="text">The value being parsed.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">Thrown if the value cannot be parsed.</exception>
        public static long Parse(string text)
        {
            if (!TryParse(text, out var value))
            {
                throw new FormatException($"Cannot parse the [{text}] {nameof(ByteUnits)}.");
            }

            return value;
        }

        /// <summary>
        /// Converts a byte count to a string using byte units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in bytes.</returns>
        public static string ToByteString(long size)
        {
            return $"{size}";
        }

        /// <summary>
        /// Converts the size to the specified units and then renders this
        /// as an invariant culture fixed point string.
        /// </summary>
        /// <param name="size">The byte size.</param>
        /// <param name="units">The units.</param>
        /// <returns>The floating point string.</returns>
        private static string ToDoubleString(long size, long units)
        {
            double doubleSize = size;

            if (units > 0)
            {
                doubleSize = (double)doubleSize / units;
            }

            return doubleSize.ToString("#0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts a byte count to a string using <b>KB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in KB.</returns>
        public static string ToKBString(long size)
        {
            return $"{ToDoubleString(size, KiloBytes)}KB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>KiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in KiB.</returns>
        public static string ToKiBString(long size)
        {
            return $"{ToDoubleString(size, KibiBytes)}KiB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>MB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in MB.</returns>
        public static string ToMBString(long size)
        {
            return $"{ToDoubleString(size, MegaBytes)}MB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>MiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in MiB.</returns>
        public static string ToMiBString(long size)
        {
            return $"{ToDoubleString(size, MebiBytes)}MiB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>GB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in GB.</returns>
        public static string ToGBString(long size)
        {
            return $"{ToDoubleString(size, GigaBytes)}GB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>GiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in GiB.</returns>
        public static string ToGiBString(long size)
        {
            return $"{ToDoubleString(size, GibiBytes)}GiB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>TB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in TB.</returns>
        public static string ToTBString(long size)
        {
            return $"{ToDoubleString(size, TeraBytes)}TB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>TiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in TiB.</returns>
        public static string ToTiBString(long size)
        {
            return $"{ToDoubleString(size, TebiBytes)}TiB";
        }

#if ALLOW_PENTA
        /// <summary>
        /// Converts a byte count to a string using <b>PB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in PB.</returns>
        public static string ToPBString(long size)
        {
            return $"{ToDoubleString(size, PentaBytes)}PB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>PiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in PiB.</returns>
        public static string ToPiBString(long size)
        {
            return $"{ToDoubleString(size, PebiBytes)}PiB";
        }
#endif
    }
}
