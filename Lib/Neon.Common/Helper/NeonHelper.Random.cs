//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Random.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        private static object                   randLock   = new object();
        private static Random                   rand       = null;
        private static RandomNumberGenerator    randCrypto = null;

        /// <summary>
        /// Returns an integer pseudo random number.
        /// </summary>
        public static int PseudoRandomInt()
        {
            lock (randLock)
            {
                if (rand == null)
                {
                    rand = new Random(Environment.TickCount ^ (int)DateTime.Now.Ticks);
                }

                return rand.Next();
            }
        }

        /// <summary>
        /// Returns a double pseudo random number between 0.0 and +1.0
        /// </summary>
        public static double PseudoRandomDouble()
        {
            lock (randLock)
            {
                if (rand == null)
                {
                    rand = new Random(Environment.TickCount ^ (int)DateTime.Now.Ticks);
                }

                return rand.NextDouble();
            }
        }

        /// <summary>
        /// Returns a double pseudo random number between 0.0 and the specified limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        public static double PseudoRandomDouble(double limit)
        {
            lock (randLock)
            {
                if (rand == null)
                {
                    rand = new Random(Environment.TickCount ^ (int)DateTime.Now.Ticks);
                }

                return rand.NextDouble() * limit;
            }
        }

        /// <summary>
        /// Returns a pseudo random number in the range of 0..limit-1.
        /// </summary>
        /// <param name="limit">The value returned will not exceed one less than this value.</param>
        /// <returns>The random number.</returns>
        public static int PseudoRandomInt(int limit)
        {
            int v = PseudoRandomInt();

            if (v == int.MinValue)
            {
                v = 0;
            }
            else if (v < 0)
            {
                v = -v;
            }

            return v % limit;
        }

        /// <summary>
        /// Returns a random index into a sequence whose length is specified.
        /// </summary>
        /// <param name="length">The sequence length.</param>
        /// <returns>The random index.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if length is &lt;= 0.</exception>
        public static int PseudoRandomIndex(int length)
        {
            if (length <= 0)
            {
                throw new IndexOutOfRangeException();
            }

            return PseudoRandomInt() % length;
        }

        /// <summary>
        /// Returns a random <see cref="TimeSpan"/> between zero and a specified maximum.
        /// </summary>
        /// <param name="maxInterval">The maximum interval.</param>
        /// <returns>The random timespan.</returns>
        /// <remarks>
        /// This method is useful for situations where its desirable to have some variation
        /// in a delay before performing an activity like retrying an operation or performing
        /// a background task.
        /// </remarks>
        public static TimeSpan PseudoRandomTimespan(TimeSpan maxInterval)
        {
            Covenant.Requires<ArgumentException>(maxInterval >= TimeSpan.Zero);

            return TimeSpan.FromSeconds(maxInterval.TotalSeconds * PseudoRandomDouble());
        }

        /// <summary>
        /// Returns a <see cref="TimeSpan"/> between the specified base interval
        /// plus a random period of the specified fraction of the value.
        /// </summary>
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
        /// baseInterval + Helper.RandTimespan(TimeSpan.FromSeconds(baseInterval.TotalSeconds * fraction));
        /// </code>
        /// <para>
        /// This method is useful for situations where its desirable to have some variation
        /// in a delay before performing an activity like retrying an operation or performing
        /// a background task.
        /// </para>
        /// </remarks>
        public static TimeSpan PseudoRandomTimespan(TimeSpan baseInterval, double fraction)
        {
            Covenant.Requires<ArgumentException>(baseInterval >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(fraction >= 0.0);

            if (fraction == 0.0)
            {
                return baseInterval;
            }

            return baseInterval + PseudoRandomTimespan(TimeSpan.FromSeconds(baseInterval.TotalSeconds * fraction));
        }

        /// <summary>
        /// Returns a random <see cref="TimeSpan" /> value between the min/max
        /// values specified.
        /// </summary>
        /// <param name="minInterval">The minimum interval.</param>
        /// <param name="maxInterval">The maximum interval.</param>
        /// <returns>The randomized time span.</returns>
        public static TimeSpan PseudoRandomTimespan(TimeSpan minInterval, TimeSpan maxInterval)
        {
            Covenant.Requires<ArgumentException>(minInterval >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(maxInterval >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(minInterval <= maxInterval);

            if (maxInterval < minInterval)
            {
                // Just being safe.

                var tmp = maxInterval;

                maxInterval = minInterval;
                minInterval = maxInterval;
            }

            return minInterval + TimeSpan.FromSeconds((maxInterval - minInterval).TotalSeconds * rand.NextDouble());
        }

        /// <summary>
        /// Generates a byte array filled with a cryptographically strong sequence of random values.
        /// </summary>
        /// <param name="count">The number of random bytes to be generated.</param>
        /// <returns>The random byte array.</returns>
        public static byte[] GetCryptoRandomBytes(int count)
        {
            Covenant.Requires<ArgumentException>(count > 0);

            var bytes = new byte[count];

            lock (randLock)
            {
                if (randCrypto == null)
                {
                    randCrypto = RandomNumberGenerator.Create();
                }

                randCrypto.GetBytes(bytes);
            }

            return bytes;
        }

        /// <summary>
        /// Generates a cryptographically random password.
        /// </summary>
        /// <param name="length">The password length.</param>
        /// <returns>The generated password.</returns>
        public static string GetCryptoRandomPassword(int length)
        {
            Covenant.Requires<ArgumentException>(length > 0);

            var sb    = new StringBuilder(length);
            var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = GetCryptoRandomBytes(length);

            foreach (var v in bytes)
            {
                sb.Append(chars[v % chars.Length]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Creates a <see cref="Random"/> pseudo random number generated
        /// with a cryptographically random seed.
        /// </summary>
        /// <returns>A <see cref="Random"/>.</returns>
        public static Random CreateSecureRandom()
        {
            var seedBytes = GetCryptoRandomBytes(4);

            return new Random(BitConverter.ToInt32(seedBytes, 0));
        }
    }
}
