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

namespace Neon.Common
{
    /// <summary>
    /// Implements a text encoding that simply reads and writes bytes without
    /// any changes.  This corresponds to the Windows codepage <b>1252</b>.
    /// </summary>
    public class ByteEncoding : Encoding
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns a <see cref="ByteEncoding"/> instance.
        /// </summary>
        public static ByteEncoding Instance { get; private set; } = new ByteEncoding();

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        private ByteEncoding()
            : base(1252)
        {
        }

        /// <inheritdoc/>
        public override int GetByteCount(char[] chars, int index, int count)
        {
            return count;
        }

        /// <inheritdoc/>
        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            for (int i = 0; i < charCount; i++)
            {
                bytes[byteIndex + i] = (byte)chars[charIndex + i];
            }

            return charCount;
        }

        /// <inheritdoc/>
        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            return count;
        }

        /// <inheritdoc/>
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            for (int i = 0; i < byteCount; i++)
            {
                chars[charIndex + i] = (char)bytes[byteIndex + i];
            }

            return byteCount;
        }

        /// <inheritdoc/>
        public override int GetMaxByteCount(int charCount)
        {
            return charCount;
        }

        /// <inheritdoc/>
        public override int GetMaxCharCount(int byteCount)
        {
            return byteCount;
        }
    }
}
