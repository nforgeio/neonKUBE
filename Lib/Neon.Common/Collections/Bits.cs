//-----------------------------------------------------------------------------
// FILE:	    Bits.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Retry;

namespace Neon.Collections
{
    /// <summary>
    /// Implements an efficient array of boolean values that can also
    /// perform bit oriented operations such as AND, OR, NOT, XOR.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This class is similar to the .NET <see cref="BitArray"/> class.  The main
    /// difference is that this class serializes the bits to a byte array as you'd
    /// expect, with bit zero being the most significant bit of the first byte,
    /// bit one being the second significant bit, etc.  The <see cref="BitArray"/>
    /// class serializes the first bit to the least significant bit of the first byte.
    /// </note>
    /// <para>
    /// A <see cref="Bits" /> bitmap encodes internally as an array of 32-bit integers which
    /// is much more memory efficent than how the .NET Framework would encode an
    /// array of boolean values.  Use the <see cref="Bits(int)" /> constructor to
    /// create a zeroed bitmap with the specified number of bits, <see cref="Bits(bool[])" />
    /// to initialize the bitmap from a boolean array, or <see cref="Bits(string)" />
    /// to create a bitmap from a string of ones and zeros, and <see cref="Bits(byte[],int)" />
    /// to load a bitmap from a byte array serialized by a previous call to <see cref="ToBytes()" />.
    /// </para>
    /// <para>
    /// You can use the indexer to get/set specific bits in the bitmap.  Note that all
    /// indexes are zero-based.  <see cref="ClearRange" /> sets the specified range of 
    /// bits to zero, <see cref="SetRange" /> sets the specified range of bits to one,
    /// and <see cref="ClearAll" /> and <see cref="SetAll" /> sets all bits to the 
    /// appropriate value.  <see cref="Resize" /> can be used to resize a bitmap.
    /// </para>
    /// <para>
    /// The class implements the following bitwise operations: <see cref="Not" />, <see cref="And" />,
    /// <see cref="Or" />, <see cref="Xor" />, <see cref="ShiftLeft" />, and <see cref="ShiftRight" />.
    /// Note that the lengths of the two bitmaps passed to binary operations must be the same.
    /// </para>
    /// <para>
    /// <see cref="Clone" /> returns a copy of the bitmap and <see cref="ToArray" /> converts the
    /// bitmap into a boolean array.  The <see cref="IsAllZeros" /> and <see cref="IsAllOnes" /> properties
    /// can be used to determine if a bitmap is all zeros or ones and <see cref="Equals" /> can be
    /// used to determine whether two bitmaps are the same.  <see cref="ToString" /> renders the bitmap
    /// as a string of 1s and 0s.
    /// </para>
    /// <para>
    /// This class also defines explict casts for converting to and from a string of ones and zeros
    /// and also defines the bitwise <b>||</b>, <b>&amp;</b>, <b>~</b> and <b>^</b>,
    /// <b>&lt;&lt;</b>, and <b>&gt;&gt;</b> operators.
    /// </para>
    /// </remarks>
    public class Bits
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// 32-bit oriented bit position masks.
        /// </summary>
        private static int[] IntBitMask;

        /// <summary>
        /// 8-bit oriented serialization bit positions masks.
        /// </summary>
        private static int[] ByteBitMasks;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Bits()
        {
            // Initialize the bit masks.

            int mask = 0x00000001;

            IntBitMask = new int[32];

            for (int i = 0; i < 32; i++)
            {
                IntBitMask[31 - i] = mask;
                mask <<= 1;
            }

            ByteBitMasks = new int[] 
            {
                0x00000080,
                0x00000040,
                0x00000020,
                0x00000010,
                0x00000008,
                0x00000004,
                0x00000002,
                0x00000001
            };
        }

        /// <summary>
        /// Casts a <see cref="Bits" /> instance into a bit string of ones and zeros.
        /// </summary>
        /// <param name="bits">The bitmap.</param>
        /// <returns>The bit string.</returns>
        public static explicit operator string(Bits bits)
        {
            return bits.ToString();
        }

        /// <summary>
        /// Casts a bit string of ones and zeros into a <see cref="Bits" /> bitmap.
        /// </summary>
        /// <param name="bitString">The bit string.</param>
        /// <returns>The bitmap.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="bitString"/> contains a character other than a 1 or 0.</exception>
        public static explicit operator Bits(string bitString)
        {
            return new Bits(bitString);
        }

        /// <summary>
        /// Returns the bitwise <b>not</b> on a bitmap.
        /// </summary>
        /// <param name="bits">The source bitmap.</param>
        /// <returns>The output bitmap.</returns>
        public static Bits operator ~(Bits bits)
        {
            return bits.Not();
        }

        /// <summary>
        /// Returns the intersection of two bitmaps.
        /// </summary>
        /// <param name="b1">The first bitmap.</param>
        /// <param name="b2">The second bitmap.</param>
        /// <returns>The intersection.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the source bitmaps don't have the same length.</exception>
        public static Bits operator &(Bits b1, Bits b2)
        {
            return b1.And(b2);
        }

        /// <summary>
        /// Returns the union of two bitmaps.
        /// </summary>
        /// <param name="b1">The first bitmap.</param>
        /// <param name="b2">The second bitmap.</param>
        /// <returns>The union.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the source bitmaps don't have the same length.</exception>
        public static Bits operator |(Bits b1, Bits b2)
        {
            return b1.Or(b2);
        }

        /// <summary>
        /// Returns the exclusive or of two bitmaps.
        /// </summary>
        /// <param name="b1">The first bitmap.</param>
        /// <param name="b2">The second bitmap.</param>
        /// <returns>The exclusive-or of the bits.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the source bitmaps don't have the same length.</exception>
        public static Bits operator ^(Bits b1, Bits b2)
        {
            return b1.Xor(b2);
        }

        /// <summary>
        /// Left shifts a bitmap by a number of positions and returns the result.
        /// </summary>
        /// <param name="input">The input bitmap.</param>
        /// <param name="count">The number of positions to shift.</param>
        /// <returns>The shifted bitmap.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="count" /> is less than zero.</exception>
        /// <remarks>
        /// <note>
        /// Any bits shifted left past position zero will be lost.
        /// </note>
        /// </remarks>
        public static Bits operator <<(Bits input, int count)
        {
            return input.ShiftLeft(count);
        }

        /// <summary>
        /// Right shifts a bitmap by a number of positions and returns the result.
        /// </summary>
        /// <param name="input">The input bitmap.</param>
        /// <param name="count">The number of positions to shift.</param>
        /// <returns>The shifted bitmap.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="count" /> is less than zero.</exception>
        /// <remarks>
        /// <note>
        /// Any bits shifted right past the end of the bitmap will be lost.
        /// </note>
        /// </remarks>
        public static Bits operator >>(Bits input, int count)
        {
            return input.ShiftRight(count);
        }

        /// <summary>
        /// Determines whether two bitmaps contain the same values.
        /// </summary>
        /// <param name="b1">The first bitmap.</param>
        /// <param name="b2">The second bitmap.</param>
        /// <returns><c>true</c> if the bitmaps are the same.</returns>
        public static bool operator ==(Bits b1, Bits b2)
        {
            bool b1Null = object.ReferenceEquals(b1, null);
            bool b2Null = object.ReferenceEquals(b2, null);

            if (b1Null && b2Null)
            {
                return true;
            }
            else if (b1Null && !b2Null)
            {
                return false;
            }
            else if (!b1Null && b2Null)
            {
                return false;
            }

            return b1.Equals(b2);
        }

        /// <summary>
        /// Determines whether two bitmaps do not contain the same values.
        /// </summary>
        /// <param name="b1">The first bitmap.</param>
        /// <param name="b2">The second bitmap.</param>
        /// <returns><c>true</c> if the bitmaps are the same.</returns>
        public static bool operator !=(Bits b1, Bits b2)
        {
            bool b1Null = object.ReferenceEquals(b1, null);
            bool b2Null = object.ReferenceEquals(b2, null);

            if (b1Null && b2Null)
            {
                return false;
            }
            else if (b1Null && !b2Null)
            {
                return true;
            }
            else if (!b1Null && b2Null)
            {
                return true;
            }

            return !b1.Equals(b2);
        }

        //---------------------------------------------------------------------
        // Implementation

        private int length;     // Logical bitmap length
        private int[] bits;     // The bitmap

        /// <summary>
        /// Constructs a zeroed bitmap of a specified length.
        /// </summary>
        /// <param name="length">The bitmap length.</param>
        /// <exception cref="ArgumentException">Thrown is <paramref name="length" /> is negative.</exception>
        public Bits(int length)
        {
            if (length < 0)
                throw new ArgumentException("Bitmap length cannot be negative.", "length");

            int c;

            if (length == 0)
            {
                c = 0;
            }
            else if (length % 32 == 0)
            {
                c = length / 32;
            }
            else
            {
                c = length / 32 + 1;
            }

            this.length = length;
            this.bits   = new int[c];
        }

        /// <summary>
        /// Constructs a bitmap from a boolean array.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="array" /> is <c>null</c>.</exception>
        public Bits(bool[] array)
        {
            int c;

            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (array.Length == 0)
            {
                c = 0;
            }
            else if (array.Length % 32 == 0)
            {
                c = array.Length / 32;
            }
            else
            {
                c = array.Length / 32 + 1;
            }

            this.length = array.Length;
            this.bits   = new int[c];

            for (int i = 0; i < array.Length; i++)
            {
                this[i] = array[i];
            }
        }

        /// <summary>
        /// Constructs a bitmap by parsing a string of 1s and 0s.
        /// </summary>
        /// <param name="bitString">The bit string.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="bitString" /> is <c>null</c>.</exception>
        /// <exception cref="FormatException">Thrown if <paramref name="bitString"/> contains a character other than a 1 or 0.</exception>
        public Bits(string bitString)
        {
            int c;

            if (bitString == null)
                throw new ArgumentNullException("bitString");

            length = bitString.Length;

            if (length == 0)
            {
                c = 0;
            }
            else if (length % 32 == 0)
            {
                c = length / 32;
            }
            else
            {
                c = length / 32 + 1;
            }

            this.bits = new int[c];

            for (int i = 0; i < length; i++)
            {
                if (bitString[i] == '0')
                {
                    this[i] = false;
                }
                else if (bitString[i] == '1')
                {
                    this[i] = true;
                }
                else
                {
                    throw new FormatException("Bit string includes an invalid character.");
                }
            }
        }

        /// <summary>
        /// Constructs a bitmap from a an array of bytes.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <remarks>
        /// This constructor is useful for deserializing bitmaps persisted to a binary structure.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="bytes" /> is <c>null</c>.</exception>
        public Bits(byte[] bytes)
            : this(bytes, bytes.Length * 8)
        {
        }

        /// <summary>
        /// Constructs a bitmap from a specified number of bits from an array of bytes.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <param name="length">
        /// Specifies length of the bitmap to be created.
        /// </param>
        /// <remarks>
        /// This constructor is useful for deserializing bitmaps persisted to a binary structure.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="bytes" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown is <paramref name="length" /> is negative.</exception>
        /// <remarks>
        /// <note>
        /// The byte array may be larger or smaller than the implied number of bits
        /// as compared to the <paramref name="length" /> parameter.
        /// </note>
        /// </remarks>
        public Bits(byte[] bytes, int length)
            : this(length)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }

            if (length < 0)
            {
                throw new ArgumentException("Bitmap length cannot be negative.", "length");
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                int b = bytes[i];

                if (b == 0)
                {
                    continue;   // No bits to set.
                }

                for (int byteBitPos = 0; byteBitPos < 8; byteBitPos++)
                {
                    if ((b & ByteBitMasks[byteBitPos]) != 0)
                    {
                        int bitMapPos = i * 8 + byteBitPos;

                        if (bitMapPos >= length)
                        {
                            return;     // No need to continue, we've reached the end of the bitmap.
                        }

                        this[bitMapPos] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the length of the bitmap.
        /// </summary>
        public int Length
        {
            get { return this.length; }
        }

        /// <summary>
        /// Gets or sets a bit in the bitmap.
        /// </summary>
        /// <param name="index">The zero-based index of the bit.</param>
        /// <returns>The bit value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Throw if the <paramref name="index" /> is not in range.</exception>
        public bool this[int index]
        {
            get
            {
                if (index < 0 || index >= this.length)
                {
                    throw new ArgumentOutOfRangeException("index");
                }

                return (bits[index / 32] & IntBitMask[index % 32]) != 0;
            }

            set
            {
                if (index < 0 || index >= this.length)
                    throw new ArgumentOutOfRangeException("index");

                if (value)
                {
                    bits[index / 32] = bits[index / 32] | IntBitMask[index % 32];
                }
                else
                {
                    bits[index / 32] = bits[index / 32] & ~IntBitMask[index % 32];
                }
            }
        }

        /// <summary>
        /// Zeros a number of bits starting at an index.
        /// </summary>
        /// <param name="index">The start index.</param>
        /// <param name="count">Number of bits.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="index"/> or <paramref name="count"/> is out of range.</exception>
        public void ClearRange(int index, int count)
        {
            int endIndex;

            if (index < 0 || index >= this.length)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            endIndex = index + count - 1;

            if (endIndex < index || endIndex >= this.length)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            // $todo(jeff.lill): 
            //
            // For now, I'm just going to clear all of the
            // bits using the indexer.  This should be optimized to
            // clear whole ints if count justifies this for performance.

            for (int i = index; i <= endIndex; i++)
            {
                this[i] = false;
            }
        }

        /// <summary>
        /// Zeros all bits.
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = 0x00000000;
            }
        }

        /// <summary>
        /// Sets a number of bits starting at an index.
        /// </summary>
        /// <param name="index">The start index.</param>
        /// <param name="count">Number of bits.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="index"/> or <paramref name="count"/> is out of range.</exception>
        public void SetRange(int index, int count)
        {
            int endIndex;

            if (index < 0 || index >= this.length)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            endIndex = index + count - 1;

            if (endIndex < index || endIndex >= this.length)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            // $todo(jeff.lill): 
            //
            // For now, I'm just going to set all of the
            // bits using the indexer.  This should be optimized to
            // clear whole ints if count justifies this for performance.

            for (int i = index; i <= endIndex; i++)
            {
                this[i] = true;
            }
        }

        /// <summary>
        /// Sets all bits.
        /// </summary>
        public void SetAll()
        {
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = unchecked((int)0xFFFFFFFF);
            }
        }

        /// <summary>
        /// Creates a new bitmap from the current instance, but resized to contain
        /// the specified number of bits.
        /// </summary>
        /// <param name="length">The length desired for the new bitmap.</param>
        /// <returns>The resized bitmap.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="length" /> is negative.</exception>
        public Bits Resize(int length)
        {
            if (length < 0)
            {
                throw new ArgumentException("Bitmap length cannot be negative.", "length");
            }

            var output = new Bits(length);

            for (int i = 0; i < Math.Min(this.bits.Length, output.bits.Length); i++)
            {
                output.bits[i] = this.bits[i];
            }

            return output;
        }

        /// <summary>
        /// Returns a bitmap that inverts all the bits of the current bitmap.
        /// </summary>
        /// <returns>The inverted <see cref="Bits" />.</returns>
        public Bits Not()
        {
            var result = new Bits(this.length);

            for (int i = 0; i < this.bits.Length; i++)
            {
                result.bits[i] = ~this.bits[i];
            }

            return result;
        }

        /// <summary>
        /// Performs a bitwise <b>and</b> on the <paramref name="bits"/> passed and the
        /// current bits and returns the result.
        /// </summary>
        /// <param name="bits">The source <see cref="Bits" />.</param>
        /// <returns>A new <see cref="Bits" /> instance with the intersection.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the source bitmaps don't have the same length.</exception>
        public Bits And(Bits bits)
        {
            if (bits.Length != this.length)
            {
                throw new InvalidOperationException("Bitmap lengths are not the same.");
            }

            Bits result = new Bits(this.length);

            for (int i = 0; i < this.bits.Length; i++)
            {
                result.bits[i] = this.bits[i] & bits.bits[i];
            }

            return result;
        }

        /// <summary>
        /// Performs a bitwise <b>or</b> on the <paramref name="bits"/> passed and the
        /// current bits and returns the result.
        /// </summary>
        /// <param name="bits">The source <see cref="Bits" />.</param>
        /// <returns>A new <see cref="Bits" /> instance with the union.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the source bitmaps don't have the same length.</exception>
        public Bits Or(Bits bits)
        {
            if (bits.Length != this.length)
                throw new InvalidOperationException("Bitmap lengths are not the same.");

            var result = new Bits(this.length);

            for (int i = 0; i < this.bits.Length; i++)
            {
                result.bits[i] = this.bits[i] | bits.bits[i];
            }

            return result;
        }

        /// <summary>
        /// Performs a bitwise <b>xor</b> on the <paramref name="bits"/> passed and the
        /// current bits and returns the result.
        /// </summary>
        /// <param name="bits">The source <see cref="Bits" />.</param>
        /// <returns>A new <see cref="Bits" /> instance with the exclusive or results.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the source bitmaps don't have the same length.</exception>
        public Bits Xor(Bits bits)
        {
            if (bits.Length != this.length)
            {
                throw new InvalidOperationException("Bitmap lengths are not the same.");
            }

            var result = new Bits(this.length);

            for (int i = 0; i < this.bits.Length; i++)
            {
                result.bits[i] = this.bits[i] ^ bits.bits[i];
            }

            return result;
        }

        /// <summary>
        /// Left shifts a bitmap by a number of positions and returns the result.
        /// </summary>
        /// <param name="count">The number of positions to shift.</param>
        /// <returns>The shifted bitmap.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="count" /> is less than zero.</exception>
        /// <remarks>
        /// <note>
        /// Any bits shifted left past position zero will be lost.
        /// </note>
        /// </remarks>
        public Bits ShiftLeft(int count)
        {
            if (count < 0)
            {
                throw new ArgumentException("Bits cannot be shifted by a negative number.", "count");
            }

            if (count == 0)
            {
                return this.Clone();
            }

            var output = new Bits(this.length);

            if (count >= this.length)
            {
                return output;      // All the bits were shifted away.
            }

            for (int i = count; i < this.length; i++)
            {
                output[i - count] = this[i];
            }

            return output;
        }

        /// <summary>
        /// Right shifts a bitmap by a number of positions and returns the result.
        /// </summary>
        /// <param name="count">The number of positions to shift.</param>
        /// <returns>The shifted bitmap.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="count" /> is less than zero.</exception>
        /// <remarks>
        /// <note>
        /// Any bits shifted right past the end of the bitmap will be lost.
        /// </note>
        /// </remarks>
        public Bits ShiftRight(int count)
        {
            if (count < 0)
            {
                throw new ArgumentException("Bits cannot be shifted by a negative number.", "count");
            }

            if (count == 0)
            {
                return this.Clone();
            }

            var output = new Bits(this.length);

            if (count >= this.length)
            {
                return output;      // All the bits were shifted away.
            }

            for (int i = 0; i < this.length - count; i++)
            {
                output[i + count] = this[i];
            }

            return output;
        }

        /// <summary>
        /// Returns a clone of the bitmap.
        /// </summary>
        /// <returns>The cloned copy.</returns>
        public Bits Clone()
        {
            var bits = new Bits(this.length);

            Array.Copy(this.bits, bits.bits, this.bits.Length);
            return bits;
        }

        /// <summary>
        /// Returns <c>true</c> if all of the bits are set to zeros.
        /// </summary>
        public bool IsAllZeros
        {
            get
            {
                //$todo(jeff.lill): Should do int operations when possible to improve performance.

                for (int i = 0; i < this.length; i++)
                {
                    if (this[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if all of the bits are set to ones.
        /// </summary>
        public bool IsAllOnes
        {
            get
            {
                //$todo(jeff.lill): Should do int operations when possible to improve performance.

                for (int i = 0; i < this.length; i++)
                {
                    if (!this[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Determines if the bitmap passed is equal to the current bitmap.
        /// </summary>
        /// <param name="obj">The instance to be compared.</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            var bits = obj as Bits;

            if (obj == null)
            {
                return false;
            }

            // $todo(jeff.lill): Should do int operations when possible to improve performance.

            if (this.length != bits.length)
            {
                return false;
            }

            for (int i = 0; i < this.length; i++)
            {
                if (this[i] != bits[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Computes a hash code for the instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Converts the bitmap into a boolean array.
        /// </summary>
        /// <returns>The boolean array.</returns>
        public bool[] ToArray()
        {
            var array = new bool[this.length];

            for (int i = 0; i < this.length; i++)
            {
                array[i] = this[i];
            }

            return array;
        }

        /// <summary>
        /// Converts the bitmap into an array of bytes.
        /// </summary>
        /// <returns>The byte array.</returns>
        /// <remarks>
        /// This method is useful for serializing bitmaps for storage in a binary structure.
        /// </remarks>
        public byte[] ToBytes()
        {
            if (length == 0)
                return new byte[0];

            byte[] bytes;

            if (length % 8 == 0)
            {
                bytes = new byte[length / 8];
            }
            else
            {
                bytes = new byte[length / 8 + 1];
            }

            for (int bitPos = 0; bitPos < length; bitPos++)
            {
                if (this[bitPos])
                {
                    var bytePos = bitPos / 8;
                    var byteBitPos = bitPos % 8;

                    bytes[bytePos] |= (byte)ByteBitMasks[byteBitPos];
                }
            }

            return bytes;
        }

        /// <summary>
        /// Renders the bitmap as a string of ones and zeros.
        /// </summary>
        /// <returns>The bitmap string.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder(this.length);

            for (int i = 0; i < this.length; i++)
            {
                sb.Append(this[i] ? '1' : '0');
            }

            return sb.ToString();
        }
    }
}
