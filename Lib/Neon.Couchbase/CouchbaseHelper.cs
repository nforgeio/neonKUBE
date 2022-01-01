//-----------------------------------------------------------------------------
// FILE:	    CouchbaseHelper.cs
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
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.N1QL;

using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Neon.Time;

namespace Couchbase
{
    /// <summary>
    /// Couchbase helper utilities.
    /// </summary>
    public static class CouchbaseHelper
    {
        /// <summary>
        /// Converts a <c>string</c> into a Couchbase literal suitable
        /// for direct inclusion into a Couchbase query string.  This
        /// handles any required quoting and character escaping.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <returns>The literal value.</returns>
        /// <remarks>
        /// <note>
        /// The string returned will always be surrounded by single quotes.
        /// </note>
        /// </remarks>
        public static string Literal(string value)
        {
            if (value == null)
            {
                return "NULL";
            }

            var sb = new StringBuilder();

            sb.Append('"');

            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"':

                        sb.Append("\\\"");
                        break;

                    case '\\':

                        sb.Append("\\\\");
                        break;

                    case '\b':

                        sb.Append("\\b");
                        break;

                    case '\f':

                        sb.Append("\\f");
                        break;

                    case '\n':

                        sb.Append("\\n");
                        break;

                    case '\r':

                        sb.Append("\\r");
                        break;

                    case '\t':

                        sb.Append("\\t");
                        break;

                    default:

                        sb.Append(ch);
                        break;
                }
            }

            sb.Append('"');

            return sb.ToString();
        }

        /// <summary>
        /// Converts a <c>string</c> into a Couchbase name suitable
        /// for direct inclusion into a Couchbase statement.  This
        /// handles any required quoting and character escaping.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <returns>The literal name.</returns>
        /// <remarks>
        /// <note>
        /// The name returned will always be surrounded by single back
        /// tick marks.
        /// </note>
        /// </remarks>
        public static string LiteralName(string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(value), nameof(value));

            var sb = new StringBuilder();

            sb.Append('`');

            // $todo(jefflill):
            //
            // I'm not entirely sure that escapes are allowed in
            // Couchbase names but I'm going to support them just
            // in case.

            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '`':

                        sb.Append("\\`");
                        break;

                    case '\\':

                        sb.Append("\\\\");
                        break;

                    default:

                        sb.Append(ch);
                        break;
                }
            }

            sb.Append('`');

            return sb.ToString();
        }

        /// <summary>
        /// Converts an <c>int</c> into a literal value.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <returns>The literal value.</returns>
        public static string Literal(int value)
        {
            return value.ToString();
        }

        /// <summary>
        /// Converts a <c>long</c> into a literal value.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <returns>The literal value.</returns>
        public static string Literal(long value)
        {
            return value.ToString();
        }

        /// <summary>
        /// Converts a <c>bool</c> into a literal value.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <returns>The literal value.</returns>
        public static string Literal(bool value)
        {
            return value ? "TRUE" : "FALSE";
        }

        /// <summary>
        /// Converts a <c>double</c> into a literal value.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <returns>The literal value.</returns>
        public static string Literal(double value)
        {
            return value.ToString("G", NumberFormatInfo.InvariantInfo);
        }
    }
}
