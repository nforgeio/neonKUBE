//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Hex.cs
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        /// <summary>
        /// Converts the byte buffer passed into a hex encoded string.
        /// </summary>
        /// <param name="buf">The buffer</param>
        /// <param name="uppercase">Optionally renders the hex digits in uppercase.</param>
        /// <returns>The hex encoded string.</returns>
        public static string ToHex(byte[] buf, bool uppercase = false)
        {
            var sb       = new StringBuilder(buf.Length * 2);
            var tenDigit = uppercase ? 'A' : 'a';

            for (int i = 0; i < buf.Length; i++)
            {
                int     v = buf[i];
                int     digit;
                char    ch;

                digit = v >> 4;

                if (digit < 10)
                {
                    ch = Convert.ToChar('0' + digit);
                }
                else
                {
                    ch = Convert.ToChar(tenDigit + digit - 10);
                }

                sb.Append(ch);

                digit = v & 0x0F;

                if (digit < 10)
                {
                    ch = Convert.ToChar('0' + digit);
                }
                else
                {
                    ch = Convert.ToChar(tenDigit + digit - 10);
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses the hex string passed and converts it a byte array.   
        /// </summary>
        /// <param name="s">The string to convert from hex.</param>
        /// <returns>The corresponding byte array.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        /// <remarks>
        /// <note>
        /// The method ignores whitespace characters 
        /// (SP,CR,LF, and TAB) in the string so that HEX strings
        /// copied directly from typical hex dump outputs can
        /// be passed directly with minimal editing.
        /// </note>
        /// </remarks>
        public static byte[] FromHex(string s)
        {
            StringBuilder sb = null;

            // Normalize the input string by removing any whitespace
            // characters.

            for (int i = 0; i < s.Length; i++)
            {
                if (Char.IsWhiteSpace(s[i]))
                {
                    sb = new StringBuilder(s.Length);
                    break;
                }
            }

            if (sb != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    if (!Char.IsWhiteSpace(s[i]))
                    {
                        sb.Append(s[i]);
                    }
                }

                s = sb.ToString();
            }

            // Parse the string.

            if ((s.Length & 1) != 0)
            {
                // Hex strings can't have an odd length

                throw new FormatException("HEX string may not have an odd length.");
            }

            byte[]  buf = new byte[s.Length / 2];
            char    ch;
            int     v1, v2;

            for (int i = 0, j = 0; i < s.Length;)
            {
                ch = s[i++];

                if ('0' <= ch && ch <= '9')
                {
                    v1 = ch - '0';
                }
                else if ('a' <= ch && ch <= 'f')
                {
                    v1 = ch - 'a' + 10;
                }
                else if ('A' <= ch && ch <= 'F')
                {
                    v1 = ch - 'A' + 10;
                }
                else
                {
                    throw new FormatException(string.Format("Invalid character [{0}] in HEX string.", ch));
                }

                ch = s[i++];

                if ('0' <= ch && ch <= '9')
                {
                    v2 = ch - '0';
                }
                else if ('a' <= ch && ch <= 'f')
                {
                    v2 = ch - 'a' + 10;
                }
                else if ('A' <= ch && ch <= 'F')
                {
                    v2 = ch - 'A' + 10;
                }
                else
                {
                    throw new FormatException(string.Format("Invalid character [{0}] in HEX string.", ch));
                }

                buf[j++] = (byte)(v1 << 4 | v2);
            }

            return buf;
        }

        /// <summary>
        /// Attempts to parse a hex string into a byte array.   
        /// </summary>
        /// <param name="s">The string to convert from hex.</param>
        /// <param name="output">Returns as the parsed byte array on success.</param>
        /// <returns><c>true</c> if the string was parsed successfully.</returns>
        /// <remarks>
        /// <note>
        /// The method ignores whitespace characters 
        /// (SP,CR,LF, and TAB) in the string so that HEX strings
        /// copied directly from typical hex dump outputs can
        /// be passed directly with minimal editing.
        /// </note>
        /// </remarks>
        public static bool TryParseHex(string s, out byte[] output)
        {
            StringBuilder sb = null;

            output = null;

            if (s == null || s.Length == 0)
            {
                return false;
            }

            // Normalize the input string by removing any whitespace
            // characters.

            for (int i = 0; i < s.Length; i++)
            {
                if (Char.IsWhiteSpace(s[i]))
                {
                    sb = new StringBuilder(s.Length);
                    break;
                }
            }

            if (sb != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    if (!Char.IsWhiteSpace(s[i]))
                    {
                        sb.Append(s[i]);
                    }
                }

                s = sb.ToString();
            }

            if (s.Length == 0)
            {
                return false;
            }

            // Parse the string.

            if ((s.Length & 1) != 0)
            {
                // Hex strings can't have an odd length

                return false;
            }

            byte[]  buf = new byte[s.Length / 2];
            char    ch;
            int     v1, v2;

            for (int i = 0, j = 0; i < s.Length; )
            {
                ch = s[i++];

                if ('0' <= ch && ch <= '9')
                {
                    v1 = ch - '0';
                }
                else if ('a' <= ch && ch <= 'f')
                {
                    v1 = ch - 'a' + 10;
                }
                else if ('A' <= ch && ch <= 'F')
                {
                    v1 = ch - 'A' + 10;
                }
                else
                {
                    return false;
                }

                ch = s[i++];

                if ('0' <= ch && ch <= '9')
                {
                    v2 = ch - '0';
                }
                else if ('a' <= ch && ch <= 'f')
                {
                    v2 = ch - 'a' + 10;
                }
                else if ('A' <= ch && ch <= 'F')
                {
                    v2 = ch - 'A' + 10;
                }
                else
                {
                    return false;
                }

                buf[j++] = (byte)(v1 << 4 | v2);
            }

            output = buf;
            return true; ;
        }

        /// <summary>
        /// Returns <c>true</c> if the character passed is a hex digit.
        /// </summary>
        /// <param name="ch">The character to test.</param>
        /// <returns><c>true</c> if the character is in one of the ranges: 0..9, a..f or A..F.</returns>
        public static bool IsHex(char ch)
        {
            return '0' <= ch && ch <= '9' || 'a' <= ch && ch <= 'f' || 'A' <= ch && ch <= 'F';
        }

        /// <summary>
        /// Converts a single byte into its hexidecimal equivalent.
        /// </summary>
        /// <param name="value">The input byte.</param>
        /// <param name="uppercase">Optionally return the hex value as uppercase.</param>
        /// <returns>The hex string.</returns>
        public static string ToHex(byte value, bool uppercase = false)
        {
            int     digit;
            char    ch1;
            char    ch2;
            char    tenDigit = uppercase ? 'A' : 'a';

            digit = value >> 4;

            if (digit < 10)
            {
                ch1 = Convert.ToChar('0' + digit);
            }
            else
            {
                ch1 = Convert.ToChar(tenDigit + digit - 10);
            }

            digit = value & 0x0F;

            if (digit < 10)
            {
                ch2 = Convert.ToChar('0' + digit);
            }
            else
            {
                ch2 = Convert.ToChar(tenDigit + digit - 10);
            }

            return new String(new char[] { ch1, ch2 });
        }

        /// <summary>
        /// Returns the decimal value of the hex digit passed.
        /// </summary>
        /// <param name="ch">The hex digit.</param>
        /// <returns>The corresponding decimal value.</returns>
        /// <remarks>
        /// Throws a FormatException if the character is not a hex digit.
        /// </remarks>
        public static int HexValue(char ch)
        {
            if ('0' <= ch && ch <= '9')
            {
                return ch - '0';
            }
            else if ('a' <= ch && ch <= 'f')
            {
                return ch - 'a' + 10;
            }
            else if ('A' <= ch && ch <= 'F')
            {
                return ch - 'A' + 10;
            }
            else
            {
                throw new FormatException("Invalid hex character.");
            }
        }

        /// <summary>
        /// Attempts to parse a hex encoded string into an integer.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="value">The parsed integer.</param>
        /// <returns><c>true</c> if the input could be parsed successfully.</returns>
        public static bool TryParseHex(string input, out int value)
        {
            int v;

            value = 0;

            if (input.Length == 0)
            {
                return false;
            }

            v = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (!IsHex(input[i]))
                {
                    return false;
                }

                v *= 16;
                v += HexValue(input[i]);
            }

            value = v;
            return true;
        }

        /// <summary>
        /// Returns a byte array as a formatted hex dump.
        /// </summary>
        /// <param name="data">The buffer to be dumped.</param>
        /// <param name="start">The first byte to be dumped.</param>
        /// <param name="count">The number of bytes to be dumped.</param>
        /// <param name="bytesPerLine">The number of bytes to dump per output line.</param>
        /// <param name="options">The formatting options.</param>
        /// <returns>The hex dump string.</returns>
        public static string HexDump(byte[] data, int start, int count, int bytesPerLine, HexDumpOption options)
        {
            var     sb = new StringBuilder();
            int     offset;
            int     pos;

            if (count == 0)
            {
                return string.Empty;
            }

            if (bytesPerLine <= 0)
            {
                throw new ArgumentException("bytesPerLine must be > 0", "bytesPerLine");
            }

            offset = 0;

            if ((options & HexDumpOption.ShowOffsets) != 0)
            {
                if (count <= 0x0000FFFF)
                {
                    sb.Append(offset.ToString("X4") + ": ");
                }
                else
                {
                    sb.Append(offset.ToString("X8") + ": ");
                }
            }

            for (pos = start; pos < start + count; )
            {
                sb.Append(data[pos].ToString("X2") + " ");

                pos++;
                offset++;
                if (offset % bytesPerLine == 0)
                {
                    if (offset != 0)
                    {
                        if ((options & HexDumpOption.ShowAnsi) != 0)
                        {
                            sb.Append("- ");
                            for (int i = pos - bytesPerLine; i < pos; i++)
                            {

                                byte v = data[i];

                                if (v < 32 || v == 0x7F)
                                {
                                    v = (byte)'.';
                                }

                                sb.Append(Encoding.ASCII.GetString(new byte[] { v }, 0, 1));
                            }
                        }

                        sb.Append("\r\n");
                    }

                    if ((options & HexDumpOption.ShowOffsets) != 0 && pos < start + count - 1)
                    {
                        if (count <= 0x0000FFFF)
                        {
                            sb.Append(offset.ToString("X4") + ": ");
                        }
                        else
                        {
                            sb.Append(offset.ToString("X8") + ": ");
                        }
                    }
                }
            }

            if ((options & HexDumpOption.ShowAnsi) != 0)
            {
                // Handle a final partial line

                int linePos = offset % bytesPerLine;

                if (linePos != 0)
                {
                    for (int i = 0; i < bytesPerLine - linePos; i++)
                    {
                        sb.Append("   ");
                    }

                    sb.Append("- ");

                    for (int i = pos - linePos; i < pos; i++)
                    {
                        byte v = data[i];

                        if (v < 32 || v == 0x7F)
                        {
                            v = (byte)'.';
                        }

                        sb.Append(Encoding.ASCII.GetString(new byte[] { v }, 0, 1));
                    }

                    sb.Append("\r\n");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns a byte array as a formatted hex dump.
        /// </summary>
        /// <param name="data">The buffer to be dumped.</param>
        /// <param name="bytesPerLine">The number of bytes to dump per output line.</param>
        /// <param name="options">The formatting options.</param>
        /// <returns>The hex dump string.</returns>
        public static string HexDump(byte[] data, int bytesPerLine, HexDumpOption options)
        {
            return HexDump(data, 0, data.Length, bytesPerLine, options);
        }
    }
}
