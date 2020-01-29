//-----------------------------------------------------------------------------
// FILE:	    ByteUnits.cs
// CONTRIBUTOR: Jeff Lill, Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

// $todo(jefflill):
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
    ///     <term><b>K</b></term>
    ///     <description>1,000</description>
    /// </item>
    /// <item>
    ///     <term><b>Ki</b></term>
    ///     <description>1,024</description>
    /// </item>
    /// <item>
    ///     <term><b>M</b></term>
    ///     <description>1000000</description>
    /// </item>
    /// <item>
    ///     <term><b>Mi</b></term>
    ///     <description>1,048,576</description>
    /// </item>
    /// <item>
    ///     <term><b>G</b></term>
    ///     <description>1,000,000,000</description>
    /// </item>
    /// <item>
    ///     <term><b>Gi</b></term>
    ///     <description>1,073,741,824</description>
    /// </item>
    /// <item>
    ///     <term><b>T</b></term>
    ///     <description>1,000,000,000,000</description>
    /// </item>
    /// <item>
    ///     <term><b>Ti</b></term>
    ///     <description>1,099,511,627,776</description>
    /// </item>
    /// <item>
    ///     <term><b>P</b></term>
    ///     <description>1,000,000,000,000,000</description>
    /// </item>
    /// <item>
    ///     <term><b>Pi</b></term>
    ///     <description>1,125,899,906,842,624</description>
    /// </item>
    /// <item>
    ///     <term><b>Ei</b></term>
    ///     <description>1,000,000,000,000,000,000‬</description>
    /// </item>
    /// <item>
    ///     <term><b>Ei</b></term>
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
            //unitLabel = unitLabel.ToUpperInvariant();

            // Map the unit label to a count.

            if (unitLabel.Length > 0)
            {
                switch (unitLabel)
                {
                    case "B":   units = 1;          break;
                    case "K":   units = KiloBytes;  break;
                    case "Ki":  units = KibiBytes;  break;
                    case "KiB": units = KibiBytes;  break;
                    case "M":   units = MegaBytes;  break;
                    case "Mi":  units = MebiBytes;  break;
                    case "MiB": units = MebiBytes;  break;
                    case "G":   units = GigaBytes;  break;
                    case "Gi":  units = GibiBytes;  break;
                    case "GiB": units = GibiBytes;  break;
                    case "T":   units = TeraBytes;  break;
                    case "Ti":  units = TebiBytes;  break;
                    case "TiB": units = TebiBytes;  break;
                    case "P":   units = PetaBytes;  break;
                    case "Pi":  units = PebiBytes;  break;
                    case "PiB": units = PebiBytes;  break;
                    case "E":   units = ExaBytes;   break;
                    case "Ei":  units = ExbiBytes;  break;
                    case "EiB": units = ExbiBytes;  break;

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
        /// Converts a byte count to a string using <b>K</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in K.</returns>
        public static string ToKString(decimal size)
        {
            return $"{ToDoubleString(size, KiloBytes)}K";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>Ki</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in Ki.</returns>
        public static string ToKiString(decimal size)
        {
            return $"{ToDoubleString(size, KibiBytes)}Ki";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>M</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in M.</returns>
        public static string ToMString(decimal size)
        {
            return $"{ToDoubleString(size, MegaBytes)}M";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>Mi</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in Mi.</returns>
        public static string ToMiString(decimal size)
        {
            return $"{ToDoubleString(size, MebiBytes)}Mi";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>G</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in G.</returns>
        public static string ToGString(decimal size)
        {
            return $"{ToDoubleString(size, GigaBytes)}G";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>Gi</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in Gi.</returns>
        public static string ToGiString(decimal size)
        {
            return $"{ToDoubleString(size, GibiBytes)}Gi";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>T</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in T.</returns>
        public static string ToTString(decimal size)
        {
            return $"{ToDoubleString(size, TeraBytes)}T";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>Ti</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in Ti.</returns>
        public static string ToTiString(decimal size)
        {
            return $"{ToDoubleString(size, TebiBytes)}Ti";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>P</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in P.</returns>
        public static string ToPString(decimal size)
        {
            return $"{ToDoubleString(size, PetaBytes)}P";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>Pi</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in Pi.</returns>
        public static string ToPiString(decimal size)
        {
            return $"{ToDoubleString(size, PebiBytes)}Pi";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>E</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in E.</returns>
        public static string ToEString(decimal size)
        {
            return $"{ToDoubleString(size, ExaBytes)}E";
        }

        /// <summary>
        /// Converts a byte count to a string using <b>Ei</b> units.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The size in Ei.</returns>
        public static string ToEiString(decimal size)
        {
            return $"{ToDoubleString(size, ExbiBytes)}Ei";
        }
    }
}
