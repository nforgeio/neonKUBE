//-----------------------------------------------------------------------------
// FILE:	    PropertyNameUtf8.cs
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
using System.ComponentModel;
using System.Text;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;
using System.Diagnostics.Contracts;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Maps a property name string to its UTF-8 form.
    /// </summary>
    internal class PropertyNameUtf8
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Computes the hash code for a byte array.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <returns>The hash code.</returns>
        public static int ComputeHash(byte[] bytes)
        {
            return ComputeHash(new Span<byte>(bytes));
        }

        /// <summary>
        /// Computes the hash code for a <c>byte</c> <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <returns>The hash code.</returns>
        public static int ComputeHash(Span<byte> bytes)
        {
            var hashCode = 0;

            for (int i = 0; i < bytes.Length; i++)
            {
                hashCode ^= (bytes[i]) << (i % 4);
            }

            return hashCode;
        }

        /// <summary>
        /// Compares a <b>byte</b> <see cref="Span{T}"/> against a <c>byte</c> array
        /// for equality.
        /// </summary>
        /// <param name="byteSpan">The byte span.</param>
        /// <param name="byteArray">The byte array.</param>
        /// <returns><c>true</c> if the items are equal.</returns>
        public static bool Equal(Span<byte> byteSpan, byte[] byteArray)
        {
            if (byteSpan.Length != byteArray.Length)
            {
                return false;
            }

            for (int i = 0; i < byteSpan.Length; i++)
            {
                if (byteSpan[i] != byteArray[i])
                {
                    return false;
                }
            }

            return true;
        }

        //---------------------------------------------------------------------
        // Instance members

        private int hashCode;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The property name string.</param>
        public PropertyNameUtf8(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.Name     = name;
            this.NameUtf8 = Encoding.UTF8.GetBytes(name);
            this.hashCode = ComputeHash(this.NameUtf8);
        }

        /// <summary>
        /// Returns the property name as a string.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the property name encoded as IUTF-8 bytes.
        /// </summary>
        public byte[] NameUtf8 { get; private set; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return hashCode;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var other = obj as PropertyNameUtf8;

            if (other == null)
            {
                return false;
            }

            if (this.hashCode != other.hashCode)
            {
                return false;
            }

            return Name.Equals(other.Name);
        }
    }
}
