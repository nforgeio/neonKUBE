//-----------------------------------------------------------------------------
// FILE:	    NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Reflection;

using Neon.Common;

using Npgsql;

namespace Neon.Postgres
{
    /// <summary>
    /// This namespace includes Postgres related extensions and utilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ConnectionExtensions"/> extends <see cref="NpgsqlConnection"/> with
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="ConnectionExtensions.OpenDatabase(NpgsqlConnection, string)"/></term>
    ///         <description>
    ///         Clones the current connection and then connection to a new database
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ConnectionExtensions.ExecuteNonQuery(NpgsqlConnection, string, NpgsqlTransaction)"/></term>
    ///         <description>
    ///         A shortcut for executing a non-query directly on a connection without
    ///         having to create a <see cref="NpgsqlCommand"/> first.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ConnectionExtensions.ExecuteScalar(NpgsqlConnection, string, NpgsqlTransaction)"/></term>
    ///         <description>
    ///         A shortcut for executing a scalar query directly on a connection without
    ///         having to create a <see cref="NpgsqlCommand"/> first.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ConnectionExtensions.ExecuteReader(NpgsqlConnection, string, System.Data.CommandBehavior, NpgsqlTransaction)"/></term>
    ///         <description>
    ///         A shortcut for executing a query directly on a connection without
    ///         having to create a <see cref="NpgsqlCommand"/> first.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ConnectionExtensions.ExecuteReaderAsync(NpgsqlConnection, string, System.Data.CommandBehavior, System.Threading.CancellationToken, NpgsqlTransaction)"/></term>
    ///         <description>
    ///         A shortcut for asynchronously executing a query directly on a connection without
    ///         having to create a <see cref="NpgsqlCommand"/> first.  This method calls an
    ///         action for each row returned by the query.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ConnectionExtensions.ExecuteBatch(NpgsqlConnection, string, NpgsqlTransaction)"/></term>
    ///         <description>
    ///         Executes a batch of SQL commands separated by <b>go</b> lines.  This allows you to have
    ///         a single script to do things like creating a table and then initializing it.  This is a
    ///         convenience that is similar to how some Microsoft SQL Server tooling works.
    ///         </description>
    ///     </item>
    /// </list>
    /// </para>
    /// <para>
    /// Asynchronous versions of these methods are also available.
    /// </para>
    /// <para>
    /// <see cref="SchemaManager"/> is designed to help manage initial database deployment as well as
    /// subsequent updates as your database schema changes over time.
    /// </para>
    /// <para>
    /// <see cref="ReaderExtensions"/> extends the <see cref="NpgsqlDataReader"/> class by adding
    /// methods to enumerate results using C# <c>foreach</c> as well as methods to fetch nullable 
    /// column values.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.CompilerGenerated]
    class NamespaceDoc
    {
    }
}
