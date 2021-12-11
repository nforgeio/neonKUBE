//-----------------------------------------------------------------------------
// FILE:	    ReaderExtensions.cs
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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

using Npgsql;
using NpgsqlTypes;

namespace Neon.Postgres
{
    /// <summary>
    /// Extends <see cref="NpgsqlDataReader"/> with useful methods.
    /// </summary>
    public static class ReaderExtensions
    {
        /// <summary>
        /// Returns an enumerator suitable for enumerating database results synchronously.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <returns>The <see cref="ReaderEnumerator"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method provides a clean way to enumerate database results using 
        /// the C# <c>foreach</c> statement or the equivalent in of the .NET languages.
        /// Here's an example:
        /// </para>
        /// <code language="c#">
        /// using (var reader = postgres.ExecuteReader("SELECT value FROM enumerate_table;"))
        /// {
        ///     foreach (var row in reader.ToEnumerable())
        ///     {
        ///         values.Add(row.GetInt32(0));
        ///     }
        /// }
        /// </code>
        /// </remarks>
        public static ReaderEnumerator ToEnumerable(this NpgsqlDataReader reader)
        {
            return new ReaderEnumerator(reader);
        }

        /// <summary>
        /// Returns an enumerator suitable for enumerating database results asynchronously.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <returns>The <see cref="ReaderAsyncEnumerator"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method provides a clean way to asynchronousl  enumerate database results 
        /// using  the C# <c>await nforeach</c> statement or the equivalent in of the .NET 
        /// languages.  Here's an example:
        /// </para>
        /// <code language="c#">
        /// using (var reader = await postgres.ExecuteReaderAsync("SELECT value FROM enumerate_table;"))
        /// {
        ///     await foreach (var row in reader.ToAsyncEnumerable())
        ///     {
        ///         values.Add(row.GetInt32(0));
        ///     }
        /// }
        /// </code>
        /// </remarks>
        public static ReaderAsyncEnumerator ToAsyncEnumerable(this NpgsqlDataReader reader)
        {
            return new ReaderAsyncEnumerator(reader);
        }

        /// <summary>
        /// Returns the column value as a string.  Unlike <see cref="NpgsqlDataReader.GetStream(int)"/>,
        /// this method can handle <c>null</c> column values.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The column string or <c>null</c>.</returns>
        public static string GetNullableString(this NpgsqlDataReader reader, int ordinal)
        {
            return reader[ordinal] as string;
        }

        /// <summary>
        /// Returns the column value as a nullable boolean.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static bool? GetNullableBoolean(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetBoolean(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable byte.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static byte? GetNullableByte(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetByte(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable character.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static char? GetNullableChar(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetChar(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="NpgsqlDate"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        [Obsolete("NpgsqlDate is obsolete")]
        public static NpgsqlDate? GetNullableDate(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetDate(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="DateTime"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static DateTime? GetNullableDateTime(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetDateTime(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="Decimal"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static Decimal? GetNullableDecimal(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetDecimal(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="double"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static double? GetNullableDouble(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetDouble(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="float"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static float? GetNullableFloat(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetFloat(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="Guid"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static Guid? GetNullableGuid(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetGuid(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="short"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static short? GetNullableInt16(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetInt16(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="int"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static int? GetNullableInt32(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetInt32(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="long"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static long? GetNullableInt64(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetInt64(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="NpgsqlTimeSpan"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        [Obsolete("NpgsqlTimeSpan is obsolete")]
        public static NpgsqlTimeSpan? GetNullableInterval(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetInterval(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        public static TimeSpan? GetNullableTimeSpan(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetTimeSpan(ordinal);
            }
        }

        /// <summary>
        /// Returns the column value as a nullable <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="reader">The data reader.</param>
        /// <param name="ordinal">The zero-based column position.</param>
        /// <returns>The nullable column value.</returns>
        [Obsolete("NpgsqlDateTime is obsolete")]
        public static NpgsqlDateTime? GetNullableTimeStamp(this NpgsqlDataReader reader, int ordinal)
        {
            if (reader[ordinal] == DBNull.Value)
            {
                return null;
            }
            else
            {
                return reader.GetTimeStamp(ordinal);
            }
        }
    }
}
