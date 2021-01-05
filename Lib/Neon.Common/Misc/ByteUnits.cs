//-----------------------------------------------------------------------------
// FILE:	    ByteUnits.cs
// CONTRIBUTOR: Jeff Lill, Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

namespace Neon.Common
{
    /// <summary>
    /// <para>
    /// Converts a byte count string with optional units into a count.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>K</b> or <b>KB</b></term>
    ///     <description>1,000</description>
    /// </item>
    /// <item>
    ///     <term><b>Ki</b> or <b>kiB</b></term>
    ///     <description>1,024</description>
    /// </item>
    /// <item>
    ///     <term><b>M</b> or <b>MB</b></term>
    ///     <description>1000000</description>
    /// </item>
    /// <item>
    ///     <term><b>Mi</b> or <b>MiB</b></term>
    ///     <description>1,048,576</description>
    /// </item>
    /// <item>
    ///     <term><b>G</b> or <b>GB</b></term>
    ///     <description>1,000,000,000</description>
    /// </item>
    /// <item>
    ///     <term><b>Gi</b> or <b>GiB</b></term>
    ///     <description>1,073,741,824</description>
    /// </item>
    /// <item>
    ///     <term><b>T</b> or <b>TB</b></term>
    ///     <description>1,000,000,000,000</description>
    /// </item>
    /// <item>
    ///     <term><b>Ti</b> or <b>TiB</b></term>
    ///     <description>1,099,511,627,776</description>
    /// </item>
    /// <item>
    ///     <term><b>P</b> or <b>PB</b></term>
    ///     <description>1,000,000,000,000,000</description>
    /// </item>
    /// <item>
    ///     <term><b>Pi</b> or <b>PiB</b></term>
    ///     <description>1,125,899,906,842,624</description>
    /// </item>
    /// <item>
    ///     <term><b>E</b> or <b>EB</b></term>
    ///     <description>1,000,000,000,000,000,000‬</description>
    /// </item>
    /// <item>
    ///     <term><b>Ei</b> or <b>EiB</b></term>
    ///     <description>1,152,921,504,606,846,976‬</description>
    /// </item>
    /// </list>
    /// </summary>
    public static class ByteUnits
    {
        /// <summary>
        /// One KB: 1,000
        /// </summary>
        public const decimal KiloBytes = 1000m;

        /// <summary>
        /// One MB: 1,000,000
        /// </summary>
        public const decimal MegaBytes = KiloBytes * KiloBytes;

        /// <summary>
        /// One GB: 1,000,000,000
        /// </summary>
        public const decimal GigaBytes = MegaBytes * KiloBytes;

        /// <summary>
        /// The constant 1,000,000,000
        /// </summary>
        public const decimal TeraBytes = GigaBytes * KiloBytes;

        /// <summary>
        /// One PB: 1,000,000,000,000
        /// </summary>
        public const decimal PetaBytes = TeraBytes * KiloBytes;

        /// <summary>
        /// One PB: 1,000,000,000,000,000
        /// </summary>
        public const decimal ExaBytes = PetaBytes * KiloBytes;

        /// <summary>
        /// One KiB: 1,024 (2^10)
        /// </summary>
        public const decimal KibiBytes = 1024m;

        /// <summary>
        /// One MiB: 1,048,576 (2^20)
        /// </summary>
        public const decimal MebiBytes = KibiBytes * KibiBytes;

        /// <summary>
        /// One GiB: 1,073,741,824 (2^30)
        /// </summary>
        public const decimal GibiBytes = MebiBytes * KibiBytes;

        /// <summary>
        /// The constant 1,099,511,627,776 (2^40)
        /// </summary>
        public const decimal TebiBytes = GibiBytes * KibiBytes;

        /// <summary>
        /// One PiB: 1,125,899,906,842,624 (2^50)
        /// </summary>
        public const decimal PebiBytes = TebiBytes * KibiBytes;

        /// <summary>
        /// One PiB: 1,152,921,504,606,846,976‬ (2^60)
        /// </summary>
        public const decimal ExbiBytes = PebiBytes * KibiBytes;

        /// <summary>
        /// Parses a floating point count string that may include one of the optional
        /// unit suffixes described here <see cref="ByteUnits"/>.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="value">Returns as the output value.</param>
        /// <returns><b>true</b> on success</returns>
        public static bool TryParse(string input, out decimal value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var units     = 1m;
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

                    case "K":
                    case "KB":  units = KiloBytes;  break;

                    case "KI":
                    case "KIB": units = KibiBytes;  break;

                    case "M":
                    case "MB":  units = MegaBytes;  break;

                    case "MI":
                    case "MIB": units = MebiBytes;  break;

                    case "G":
                    case "GB":  units = GigaBytes;  break;

                    case "GI":
                    case "GIB": units = GibiBytes;  break;

                    case "T":  
                    case "TB":  units = TeraBytes; break;

                    case "TI":  
                    case "TIB": units = TebiBytes;  break;

                    case "P":
                    case "PB":  units = PetaBytes;  break;

                    case "PI":  
                    case "PIB": units = PebiBytes;  break;

                    case "E":
                    case "EB":  units = ExaBytes;   break;

                    case "EI":
                    case "EIB": units = ExbiBytes;  break;

                    default:

                        // Unknown units

                        return false;
                }
            }

            if (unitLabel.Length > 0)
            {
                input = input.Substring(0, input.Length - unitLabel.Length);
            }

            if (!decimal.TryParse(input.Trim(), NumberStyles.Any, NumberFormatInfo.InvariantInfo, out var raw))
            {
                return false;
            }

            value = (decimal)(raw * units);

            return value >= 0.0m;
        }

        /// <summary>
        /// Parses a byte count and returns a <c>decimal</c>.
        /// </summary>
        /// <param name="text">The value being parsed.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">Thrown if the value cannot be parsed.</exception>
        public static decimal Parse(string text)
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
        public static string ToByteString(decimal size)
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
        private static string ToDoubleString(decimal size, decimal units)
        {
            double doubleSize = (double)size;

            if (units > 0)
            {
                doubleSize = doubleSize / (double)units;
            }

            return doubleSize.ToString("#0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts a byte count to a string using <b>KB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in KB.</returns>
        public static string ToKB(decimal size)
        {
            return $"{ToDoubleString(size, KiloBytes)}KB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>KiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in KiB.</returns>
        public static string ToKiB(decimal size)
        {
            return $"{ToDoubleString(size, KibiBytes)}KiB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>MB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in MB.</returns>
        public static string ToMB(decimal size)
        {
            return $"{ToDoubleString(size, MegaBytes)}MB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>MiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in MiB.</returns>
        public static string ToMiB(decimal size)
        {
            return $"{ToDoubleString(size, MebiBytes)}MiB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>GB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in GB.</returns>
        public static string ToGB(decimal size)
        {
            return $"{ToDoubleString(size, GigaBytes)}GB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>GiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in GiB.</returns>
        public static string ToGiB(decimal size)
        {
            return $"{ToDoubleString(size, GibiBytes)}GiB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>TB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in TB.</returns>
        public static string ToTB(decimal size)
        {
            return $"{ToDoubleString(size, TeraBytes)}TB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>TiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in TiB.</returns>
        public static string ToTiB(decimal size)
        {
            return $"{ToDoubleString(size, TebiBytes)}TiB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>PB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in PB.</returns>
        public static string ToPB(decimal size)
        {
            return $"{ToDoubleString(size, PetaBytes)}PB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>PiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in PiB.</returns>
        public static string ToPiB(decimal size)
        {
            return $"{ToDoubleString(size, PebiBytes)}PiB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>EB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in EB.</returns>
        public static string ToEB(decimal size)
        {
            return $"{ToDoubleString(size, ExaBytes)}EB";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>EiB</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in Ei.</returns>
        public static string ToEiB(decimal size)
        {
            return $"{ToDoubleString(size, ExbiBytes)}EiB";
        }
    }
}
