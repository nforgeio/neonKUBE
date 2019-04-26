//-----------------------------------------------------------------------------
// FILE:	    IOExtensions.cs
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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.IO
{
    /// <summary>
    /// Implements I/O related class extensions.
    /// </summary>
    public static class IOExtensions
    {
        //---------------------------------------------------------------------
        // Stream extensions

        /// <summary>
        /// Writes a byte array to a stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="bytes">The byte array.</param>
        public static void Write(this Stream stream, byte[] bytes)
        {
            Covenant.Requires<ArgumentNullException>(stream != null);
            Covenant.Requires<ArgumentNullException>(bytes != null);

            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Asynchronously writes a byte array to a stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="bytes">The byte array.</param>
        public static async Task WriteAsync(this Stream stream, byte[] bytes)
        {
            Covenant.Requires<ArgumentNullException>(stream != null);
            Covenant.Requires<ArgumentNullException>(bytes != null);

            await stream.WriteAsync(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Reads the byte array from the current position, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="cb">The number of bytes to read.</param>
        /// <returns>
        /// The byte array.  Note that the array returned may have a length
        /// less than the size requested if the end of the file has been
        /// reached.
        /// </returns>
        public static byte[] ReadBytes(this Stream stream, int cb)
        {
            byte[]  buf;
            byte[]  temp;
            int     cbRead;

            buf    = new byte[cb];
            cbRead = stream.Read(buf, 0, cb);

            if (cbRead == cb)
            {
                return buf;
            }

            temp = new byte[cbRead];
            Array.Copy(buf, temp, cbRead);
            return temp;
        }

        /// <summary>
        /// Reads all bytes from the current position to the end of the stream.
        /// </summary>
        /// <returns>The byte array.</returns>
        public static byte[] ReadToEnd(this Stream stream)
        {
            Covenant.Requires<ArgumentNullException>(stream != null);

            var buffer = new byte[64 * 1024];

            using (var ms = new MemoryStream(64 * 1024))
            {
                while (true)
                {
                    var cb = stream.Read(buffer, 0, buffer.Length);

                    if (cb == 0)
                    {
                        return ms.ToArray();
                    }

                    ms.Write(buffer, 0, cb);
                }
            }
        }

        /// <summary>
        /// Asynchronously reads all bytes from the current position to the end of the stream.
        /// </summary>
        /// <returns>The byte array.</returns>
        public static async Task<byte[]> ReadToEndAsync(this Stream stream)
        {
            Covenant.Requires<ArgumentNullException>(stream != null);

            var buffer = new byte[16 * 1024];

            using (var ms = new MemoryStream(16 * 1024))
            {
                while (true)
                {
                    var cb = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (cb == 0)
                    {
                        return ms.ToArray();
                    }

                    ms.Write(buffer, 0, cb);
                }
            }
        }

        /// <summary>
        /// Uses deflate to compress a source to a target stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        public static void DeflateTo(this Stream source, Stream target)
        {
            Covenant.Requires<ArgumentNullException>(source != null);
            Covenant.Requires<ArgumentNullException>(target != null);

            using (var compressor = new DeflateStream(target, CompressionMode.Compress))
            {
                source.CopyTo(compressor);
            }
        }

        /// <summary>
        /// Uses deflate to decompress a source to a target stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        public static void InflateTo(this Stream source, Stream target)
        {
            Covenant.Requires<ArgumentNullException>(source != null);
            Covenant.Requires<ArgumentNullException>(target != null);

            using (var decompressor = new DeflateStream(source, CompressionMode.Decompress))
            {
                decompressor.CopyTo(target);
            }
        }

        /// <summary>
        /// Uses GZIP to compress a source to a target stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        public static void GzipTo(this Stream source, Stream target)
        {
            Covenant.Requires<ArgumentNullException>(source != null);
            Covenant.Requires<ArgumentNullException>(target != null);

            using (var compressor = new GZipStream(target, CompressionLevel.Optimal))
            {
                source.CopyTo(compressor);
            }
        }

        /// <summary>
        /// Uses GZIP to decompress a source to a target stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="target">The target stream.</param>
        public static void GunzipTo(this Stream source, Stream target)
        {
            Covenant.Requires<ArgumentNullException>(source != null);
            Covenant.Requires<ArgumentNullException>(target != null);

            using (var decompressor = new GZipStream(source, CompressionMode.Decompress))
            {
                decompressor.CopyTo(target);
            }
        }

        //---------------------------------------------------------------------
        // TextReader extensions

        /// <summary>
        /// Returns an enumerator that returns the lines of text from a <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="ignoreBlank">Optionally skip empty lines or lines with oly whitespace.</param>
        /// <returns>The <see cref="IEnumerable{String}"/>.</returns>
        public static IEnumerable<string> Lines(this TextReader reader, bool ignoreBlank = false)
        {
            Covenant.Requires<ArgumentNullException>(reader != null);

            for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                if (ignoreBlank && line.Trim().Length == 0)
                {
                    continue;
                }

                yield return line;
            }
        }
    }
}
