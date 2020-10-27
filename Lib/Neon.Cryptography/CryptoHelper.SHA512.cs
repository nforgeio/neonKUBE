//-----------------------------------------------------------------------------
// FILE:	    CryptoHelper.SHA512.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Cryptography
{
    public static partial class CryptoHelper
    {
        /// <summary>
        /// The  number of bytes in a SHA512 hash.
        /// </summary>
        public const int SHA512ByteSize = 64;

        /// <summary>
        /// Computes the SHA512 hash for a string and returns the result
        /// formatted as a lowercase hex string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The hash HEX string.</returns>
        public static string ComputeSHA512String(string input)
        {
            return NeonHelper.ToHex(ComputeSHA512Bytes(input));
        }

        /// <summary>
        /// Computes the SHA512 hash for a byte array and returns the result
        /// formatted as a lowercase hex string.
        /// </summary>
        /// <param name="input">The input bytes.</param>
        /// <returns>The hash HEX string.</returns>
        public static string ComputeSHA512String(byte[] input)
        {
            return NeonHelper.ToHex(ComputeSHA512Bytes(input));
        }

        /// <summary>
        /// Computes the SHA512 hash for a stream from the current position'
        /// until the end and returns the result formatted as a lowercase 
        /// hex string.
        /// </summary>
        /// <param name="input">The stream.</param>
        /// <returns>The hash HEX string.</returns>
        public static string ComputeSHA512String(Stream input)
        {
            return NeonHelper.ToHex(ComputeSHA512Bytes(input));
        }

        /// <summary>
        /// Computes the SHA512 hash for a string and returns the result
        /// as a byte array.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The hash bytes.</returns>
        public static byte[] ComputeSHA512Bytes(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new byte[SHA512ByteSize];
            }

            return ComputeSHA512Bytes(Encoding.UTF8.GetBytes(input));
        }

        /// <summary>
        /// Computes the SHA512 hash for a byte array and returns the result
        /// as a byte array.
        /// </summary>
        /// <param name="input">The input bytes.</param>
        /// <returns>The hash bytes.</returns>
        public static byte[] ComputeSHA512Bytes(byte[] input)
        {
            if (input == null || input.Length == 0)
            {
                return new byte[SHA512ByteSize];
            }

            using (var hasher = SHA512.Create())
            {
                return hasher.ComputeHash(input);
            }
        }

        /// <summary>
        /// Computes the SHA512 hash for a stream from the current position'
        /// until the end and returns the result formatted as a lowercase 
        /// hex string.
        /// </summary>
        /// <param name="input">The stream.</param>
        /// <returns>The hash HEX string.</returns>
        public static byte[] ComputeSHA512Bytes(Stream input)
        {
            Covenant.Requires<ArgumentNullException>(input != null, nameof(input));

            var startPos = input.Position;

            using (var hasher = SHA512.Create())
            {
                var hash = hasher.ComputeHash(input);

                if (input.Position == startPos)
                {
                    // There was no data.

                    return new byte[SHA512ByteSize];
                }
                else
                {
                    return hash;
                }
            }
        }
    }
}
