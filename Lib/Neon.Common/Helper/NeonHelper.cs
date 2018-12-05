//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    /// Provides global common utilities and state.
    /// </summary>
    public static partial class NeonHelper
    {
        /// <summary>
        /// Used for thread synchronization.
        /// </summary>
        private static object syncRoot = new object();

        /// <summary>
        /// Ordinal value of an ASCII carriage return.
        /// </summary>
        public const int CR = 0x0D;

        /// <summary>
        /// Ordinal value of an ASCII linefeed.
        /// </summary>
        public const int LF = 0x0A;

        /// <summary>
        /// Ordinal value of an ASCII horizontal TAB.
        /// </summary>
        public const int HT = 0x09;

        /// <summary>
        /// Ordinal value of an ASCII escape character.
        /// </summary>
        public const int ESC = 0x1B;

        /// <summary>
        /// Ordinal value of an ASCII TAB character.
        /// </summary>
        public const int TAB = 0x09;

        /// <summary>
        /// A string consisting of a CRLF sequence.
        /// </summary>
        public const string CRLF = "\r\n";

        /// <summary>
        /// Returns the native text line ending for the current environment.
        /// </summary>
        public static readonly string LineEnding = IsWindows ? "\r\n" : "\n";

        /// <summary>
        /// The constant 1,024 (2^10).
        /// </summary>
        public const long Kilo = 1024;

        /// <summary>
        /// The constant 1,048,576 (2^20).
        /// </summary>
        public const long Mega = Kilo * Kilo;

        /// <summary>
        /// The constant 1,073,741,824 (2^30).
        /// </summary>
        public const long Giga = Mega * Kilo;

        /// <summary>
        /// The constant 1,099,511,627,776 (2^40).
        /// </summary>
        public const long Tera = Giga * Kilo;

        /// <summary>
        /// Returns the characters used as wildcards for the current file system.
        /// </summary>
        public static char[] FileWildcards { get; private set; } = new char[] { '*', '?' };

        /// <summary>
        /// Returns the date format string used for serialize dates to
        /// strings like: <b>2018-06-05T14:30:13.000Z</b>
        /// </summary>
        public const string DateFormatTZ = "yyyy-MM-ddTHH:mm:ss.fffZ";

        /// <summary>
        /// Returns the date format string used for serialize dates to
        /// strings like: <b>2018-06-05T14:30:13.000+00:00</b>
        /// </summary>
        public const string DateFormatTZOffset = "yyyy-MM-ddTHH:mm:ss.fff+00:00";

        /// <summary>
        /// Returns the Unix epoch time: 01-01-1970 (UTC).
        /// </summary>
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Indicates whether the current application was built as 32 or 64-bit or <c>null</c>
        /// if this hasn't been determined yet.
        /// </summary>
        private static bool? is64Bit;

        /// <summary>
        /// Indicates whether the current application is running on a developer workstation
        /// or <c>null</c> if this hasn't been determined yet.  This is determined by the
        /// presence of the <b>DEV_WORKSTATION</b> environment variable.
        /// </summary>
        private static bool? isDevWorkstation;

        /// <summary>
        /// The <see cref="Neon.Common.ServiceContainer"/> instance returned by 
        /// <see cref="ServiceContainer"/>.
        /// </summary>
        private static ServiceContainer serviceContainer;

        /// <summary>
        /// Set to <c>true</c> when the special UTF-8 encoding provider with the misspelled
        /// name <b>utf8</b> (without the dash) has been initialized.  See 
        /// <see cref="RegisterMisspelledUtf8Provider()"/> for more information.
        /// </summary>
        private static bool specialUtf8EncodingProvider = false;

        /// <summary>
        /// The root dependency injection service container used by Neon class libraries. 
        /// and applications.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This instance implements both the <see cref="IServiceCollection"/> and <see cref="IServiceProvider"/>
        /// interfaces and supports adding, removing, and locating services over the lifetime
        /// of the application.  This is more flexible than the default Microsoft injection
        /// pattern, where services are added to an <see cref="IServiceCollection"/> at startup
        /// and then a read-only snapshot is taken via a <b>BuildServiceProvider()</b> call
        /// that is used throughout the remaining application lifespan.
        /// </para>
        /// <para>
        /// This is implemented by a <see cref="ServiceCollection"/> by default.  It is possible
        /// to replace this very early during application initialization but the default 
        /// implementation should suffice for most purposes.
        /// </para>
        /// </remarks>
        public static ServiceContainer ServiceContainer
        {
            get
            {
                lock (syncRoot)
                {
                    if (serviceContainer == null)
                    {
                        serviceContainer = new ServiceContainer();
                    }

                    return serviceContainer;
                }
            }

            set { serviceContainer = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if the application was built as 64-bit.
        /// </summary>
        public static bool Is64Bit
        {
            get
            {
                if (is64Bit.HasValue)
                {
                    return is64Bit.Value;
                }

                is64Bit = System.Runtime.InteropServices.Marshal.SizeOf<IntPtr>() == 8;

                return is64Bit.Value;
            }
        }

        /// <summary>
        /// Indicates whether the current application is running on a developer workstation.
        /// This is determined by the presence of the <b>DEV_WORKSTATION</b> environment variable.
        /// </summary>
        public static bool IsDevWorkstation
        {
            get
            {
                if (isDevWorkstation.HasValue)
                {
                    return isDevWorkstation.Value;
                }

                isDevWorkstation = Environment.GetEnvironmentVariable("DEV_WORKSTATION") != null;

                return isDevWorkstation.Value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the library was built as 32-bit.
        /// </summary>
        public static bool Is32BitBuild
        {
            get { return !Is64Bit; }
        }

        /// <summary>
        /// Ensures that a special UTF-8 text encoding provider misnamed as <b>utf8</b>
        /// (without the dash) is registered.  This is required sometimes because
        /// certain REST APIs may return incorrect <b>charset</b> values.
        /// </summary>
        public static void RegisterMisspelledUtf8Provider()
        {
            lock (syncRoot)
            {
                if (specialUtf8EncodingProvider)
                {
                    return;
                }

                Encoding.RegisterProvider(new SpecialUtf8EncodingProvider());
                specialUtf8EncodingProvider = true;
            }
        }

        /// <summary>
        /// Parses a floating point count string that may include one of the following unit
        /// suffixes: <b>B</b>, <b>K</b>, <b>KB</b>, <b>M</b>, <b>MB</b>, <b>G</b>,
        /// <b>GB</b>, <b>T</b>, or <b>TB</b>.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="value">Returns as the output value.</param>
        /// <returns><b>true</b> on success</returns>
        public static bool TryParseCount(string input, out double value)
        {
            value = 0.0;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var units = 1L;
            var trim  = 0;

            if (input.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
            {
                units = Kilo;
                trim  = 2;
            }
            else if (input.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
            {
                units = Mega;
                trim  = 2;
            }
            else if (input.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
            {
                units = Giga;
                trim  = 2;
            }
            else if (input.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
            {
                units = Tera;
                trim  = 2;
            }
            else if (input.EndsWith("B", StringComparison.OrdinalIgnoreCase))
            {
                units = 1;
                trim  = 1;
            }
            else if (input.EndsWith("K", StringComparison.OrdinalIgnoreCase))
            {
                units = Kilo;
                trim  = 1;
            }
            else if (input.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                units = Mega;
                trim  = 1;
            }
            else if (input.EndsWith("G", StringComparison.OrdinalIgnoreCase))
            {
                units = Giga;
                trim  = 1;
            }
            else if (input.EndsWith("T", StringComparison.OrdinalIgnoreCase))
            {
                units = Tera;
                trim  = 1;
            }

            if (trim != 0)
            {
                input = input.Substring(0, input.Length - trim);
            }

            if (!double.TryParse(input.Trim(), NumberStyles.Any, NumberFormatInfo.InvariantInfo, out var raw))
            {
                return false;
            }

            value = raw * units;

            return value >= 0.0;
        }
    }
}
