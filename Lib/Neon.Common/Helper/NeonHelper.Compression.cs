//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Compression.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.Common
{
    public static partial class NeonHelper
    {

        /// <summary>
        /// Uses deflate to commpress a string.
        /// </summary>
        /// <param name="input">The input string or <c>null</c>.</param>
        /// <returns>The compressed bytes or <c>null</c>.</returns>
        public static byte[] DeflateString(string input)
        {
            if (input == null)
            {
                return null;
            }

            using (var msCompressed = new MemoryStream())
            {
                using (var compressor = new DeflateStream(msCompressed, CompressionLevel.Optimal))
                {
                    compressor.Write(Encoding.UTF8.GetBytes(input));
                }

                return msCompressed.ToArray();
            }
        }

        /// <summary>
        /// Uses deflate to decompress a string from compressed bytes.
        /// </summary>
        /// <param name="bytes">The compressed bytes or <c>null</c>.</param>
        /// <returns>The decompressed string or <c>null</c>.</returns>
        public static string InflateString(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msUncompressed = new MemoryStream())
            {
                using (var msCompressed = new MemoryStream(bytes))
                {
                    using (var decompressor = new DeflateStream(msCompressed, CompressionMode.Decompress))
                    {
                        decompressor.CopyTo(msUncompressed);
                    }

                    return Encoding.UTF8.GetString(msUncompressed.ToArray());
                }
            }
        }

        /// <summary>
        /// Uses deflate to commpress a byte array.
        /// </summary>
        /// <param name="bytes">The input byte array or <c>null</c>.</param>
        /// <returns>The compressed bytes or <c>null</c>.</returns>
        public static byte[] DeflateBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msCompressed = new MemoryStream())
            {
                using (var compressor = new DeflateStream(msCompressed, CompressionLevel.Optimal))
                {
                    compressor.Write(bytes);
                }

                return msCompressed.ToArray();
            }
        }

        /// <summary>
        /// Uses deflate to decompress a byte array from compressed bytes.
        /// </summary>
        /// <param name="bytes">The compressed bytes or <c>null</c>.</param>
        /// <returns>The decompressed string or <c>null</c>.</returns>
        public static byte[] InflateBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msUncompressed = new MemoryStream())
            {
                using (var msCompressed = new MemoryStream(bytes))
                {
                    using (var decompressor = new DeflateStream(msCompressed, CompressionMode.Decompress))
                    {
                        decompressor.CopyTo(msUncompressed);
                    }

                    return msUncompressed.ToArray();
                }
            }
        }

        /// <summary>
        /// Examines a file to determine whether it has been compressed via GZIP.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns><c>true</c> if the file is compressed via GZIP.</returns>
        public static bool IsGzipped(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return IsGzipped(stream);
            }
        }

        /// <summary>
        /// Examines a <see cref="Stream"/> to determine whether it has been compressed via GZIP.
        /// This assumes that the current position points to the GZIP header if there is one.
        /// The stream position will be restored before returning.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns><c>true</c> if the file is compressed via GZIP.</returns>
        public static bool IsGzipped(Stream stream)
        {
            Covenant.Requires<ArgumentNullException>(stream != null);

            // GZIP files begin with the two byte magic number:
            //
            //      0x1f 0x8b

            var orgPos = stream.Position;

            try
            {
                if (stream.Length - stream.Position < 2)
                {
                    return false;
                }

                if (stream.ReadByte() != 0x1f)
                {
                    return false;
                }

                return stream.ReadByte() == 0x8b;
            }
            finally
            {
                stream.Position = orgPos;
            }
        }

        /// <summary>
        /// Uses GZIP to commpress a string.
        /// </summary>
        /// <param name="input">The input string or <c>null</c>.</param>
        /// <returns>The compressed bytes or <c>null</c>.</returns>
        public static byte[] GzipString(string input)
        {
            if (input == null)
            {
                return null;
            }

            using (var msCompressed = new MemoryStream())
            {
                using (var compressor = new GZipStream(msCompressed, CompressionLevel.Optimal))
                {
                    compressor.Write(Encoding.UTF8.GetBytes(input));
                }

                return msCompressed.ToArray();
            }
        }

        /// <summary>
        /// Uses GZIP to decompress a string from compressed bytes.
        /// </summary>
        /// <param name="bytes">The compressed bytes or <c>null</c>.</param>
        /// <returns>The decompressed string or <c>null</c>.</returns>
        public static string GunzipString(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msUncompressed = new MemoryStream())
            {
                using (var msCompressed = new MemoryStream(bytes))
                {
                    using (var decompressor = new GZipStream(msCompressed, CompressionMode.Decompress))
                    {
                        decompressor.CopyTo(msUncompressed);
                    }

                    return Encoding.UTF8.GetString(msUncompressed.ToArray());
                }
            }
        }

        /// <summary>
        /// Uses GZIP to commpress a byte array.
        /// </summary>
        /// <param name="bytes">The input byte array or <c>null</c>.</param>
        /// <returns>The compressed bytes or <c>null</c>.</returns>
        public static byte[] GzipBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msCompressed = new MemoryStream())
            {
                using (var compressor = new GZipStream(msCompressed, CompressionLevel.Optimal))
                {
                    compressor.Write(bytes);
                }

                return msCompressed.ToArray();
            }
        }

        /// <summary>
        /// Uses GZIP to decompress a byte array from compressed bytes.
        /// </summary>
        /// <param name="bytes">The compressed bytes or <c>null</c>.</param>
        /// <returns>The decompressed string or <c>null</c>.</returns>
        public static byte[] GunzipBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msUncompressed = new MemoryStream())
            {
                using (var msCompressed = new MemoryStream(bytes))
                {
                    using (var decompressor = new GZipStream(msCompressed, CompressionMode.Decompress))
                    {
                        decompressor.CopyTo(msUncompressed);
                    }

                    return msUncompressed.ToArray();
                }
            }
        }
    }
}
