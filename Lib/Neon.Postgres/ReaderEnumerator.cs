//-----------------------------------------------------------------------------
// FILE:	    ReaderEnumerator.cs
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
using System.Collections;
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
    /// Returned by <see cref="ReaderExtensions.ToEnumerable(NpgsqlDataReader)"/> making
    /// it possible to synchronously enumerate the reader rows via the C# <c>foreach</c> 
    /// statement or the equivalent for other .NET languages.
    /// </summary>
    public struct ReaderEnumerator : IEnumerable<NpgsqlDataReader>
    {
        private NpgsqlDataReader reader;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="reader">The reader we'll be enumerating.</param>
        internal ReaderEnumerator(NpgsqlDataReader reader)
        {
            Covenant.Requires<ArgumentNullException>(reader != null, nameof(reader));

            this.reader = reader;
        }

        /// <inheritdoc/>
        public IEnumerator<NpgsqlDataReader> GetEnumerator()
        {
            if (reader == null)
            {
                throw new InvalidOperationException($"You may only enumerate a [{nameof(NpgsqlDataReader)}] one time.");
            }

            while (reader.Read())
            {
                yield return reader;
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
