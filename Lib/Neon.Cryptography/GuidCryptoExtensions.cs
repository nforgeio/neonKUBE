//-----------------------------------------------------------------------------
// FILE:	    GuidExtensions.cs
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;

namespace System
{
    /// <summary>
    /// Implements <see cref="Guid"/> extensions.
    /// </summary>
    public static class GuidCryptoExtensions
    {
        /// <summary>
        /// Returns the 32-character lowecase HEX representiation of the GUID.
        /// </summary>
        /// <param name="guid">The GUID.</param>
        /// <returns>The 16-byte hash.</returns>
        public static string ToHex(this Guid guid)
        {
            return NeonHelper.ToHex(guid.ToByteArray(), uppercase: false);
        }

        /// <summary>
        /// Computes and returns the 16-byte MD5 hash of the GUID.
        /// </summary>
        /// <param name="guid">The GUID.</param>
        /// <returns>The 16-byte hash.</returns>
        public static byte[] ToMd5Bytes(this Guid guid)
        {
            return CryptoHelper.ComputeMD5Bytes(guid.ToByteArray());
        }

        /// <summary>
        /// Computes and returns the 32-character lowercase HEX MD5 hash of the GUID.
        /// </summary>
        /// <param name="guid">The GUID.</param>
        /// <returns>The hash hex characters.</returns>
        public static string ToMd5Hex(this Guid guid)
        {
            return NeonHelper.ToHex(guid.ToMd5Bytes(), uppercase: false);
        }

        /// <summary>
        /// Computes and returns the 8-byte folded MD5 hash of the GUID.
        /// </summary>
        /// <param name="guid">The GUID.</param>
        /// <returns>The 8-byte hash.</returns>
        /// <remarks>
        /// <para>
        /// This works by:
        /// </para>
        /// <list type="number">
        /// <item>Generating the 16-byte MD5 hash of the GUID.</item>
        /// <item>Splitting the result in half to two 8-byte arrays.</item>
        /// <item>XOR-ing the bytes of the two arrays to form a new 8-byte array.</item>
        /// <item>Retuning the result.</item>
        /// </list>
        /// </remarks>
        public static byte[] ToFoldedBytes(this Guid guid)
        {
            var md5Hash = CryptoHelper.ComputeMD5Bytes(guid.ToByteArray());
            var result  = new byte[8];  // $note(jefflill): I'm not actually splitting into two arrays.

            for (int i = 0; i < 8; i++)
            {
                result[i] = (byte)(md5Hash[i] ^ md5Hash[i + 8]);
            }

            return result;
        }

        /// <summary>
        /// Computes and returns the 16-character lowercase HEX folded MD5 hash of the GUID.
        /// </summary>
        /// <param name="guid">The GUID.</param>
        /// <returns>The 8-byte hash.</returns>
        /// <remarks>
        /// <para>
        /// This works by:
        /// </para>
        /// <list type="number">
        /// <item>Generating the 16-byte MD5 hash of the GUID.</item>
        /// <item>Splitting the result in half to two 8-byte arrays.</item>
        /// <item>XOR-ing the bytes of the two arrays to form a new 8-byte array.</item>
        /// <item>Retuning the result as lowercase HEX.</item>
        /// </list>
        /// </remarks>
        public static string ToFoldedHex(this Guid guid)
        {
            return NeonHelper.ToHex(guid.ToFoldedBytes(), uppercase: false);
        }
    }
}
