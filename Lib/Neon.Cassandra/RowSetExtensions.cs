//-----------------------------------------------------------------------------
// FILE:	    SessionExtensions.cs
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

using Cassandra;

namespace Neon.Cassandra
{
    /// <summary>
    /// Extends the Cassandra <see cref="RowSet"/> class.
    /// </summary>
    public static class RowSetExtensions
    {
        /// <summary>
        /// <para>
        /// Distructively tests a <see cref="RowSet"/> to see if it has any rows.  It does
        /// this by trying to fetch the first row and returning <c>true</c> when there was
        /// a row or <c>false</c> when there wasn't.
        /// </para>
        /// <para>
        /// This means that if you enumerate the rows after calling this, that the first 
        /// row returned by the database won't be included in the enumeration.  This is what
        /// we mean by <i>distructive</i>.
        /// </para>
        /// </summary>
        /// <param name="rowSet">The row set.</param>
        public static bool HasRows(this RowSet rowSet)
        {
            var row = (Row)null;

            foreach (var r in rowSet)
            {
                row = r;
                break;
            }

            return row != null;
        }
    }
}
