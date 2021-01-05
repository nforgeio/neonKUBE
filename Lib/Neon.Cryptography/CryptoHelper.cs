//-----------------------------------------------------------------------------
// FILE:	    CryptoHelper.cs
// CONTRIBUTOR: Jeff Lill
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
    /// <summary>
    /// Crypography related helper methods.
    /// </summary>
    public static partial class CryptoHelper
    {
        /// <summary>
        /// The size of an HMAC256 in bytes.
        /// </summary>
        public const int HMAC256ByteCount = 256 / 8;

        /// <summary>
        /// The size of an HMAC512 in bytes.
        /// </summary>
        public const int HMAC512ByteCount = 512 / 8;

        /// <summary>
        /// Generates a symmetric encryption key from a password string.
        /// </summary>
        /// <param name="password">The input password.</param>
        /// <param name="keySize">
        /// The desired key size in bits (this must be less than or 
        /// equal to 512 and be a factor of 8).
        /// </param>
        /// <returns>The derived key.</returns>
        public static byte[] DeriveKeyFromPassword(string password, int keySize)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(password), nameof(password));
            Covenant.Requires<ArgumentException>(0 < keySize && keySize <= 512 && keySize % 8 == 0, nameof(keySize));

            // We're going to generate a SHA512 hash from the password and then
            // extract the required number of bytes from the the beginning of
            // the hash output.

            var hash = ComputeSHA512Bytes(password);
            var key  = new byte[keySize / 8];

            for (int i = 0; i < key.Length; i++)
            {
                key[i] = hash[i];
            }

            return key;
        }
    }
}
