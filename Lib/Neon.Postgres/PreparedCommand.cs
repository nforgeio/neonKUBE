//-----------------------------------------------------------------------------
// FILE:	    PreparedCommand.cs
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
using System.Data;
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
    /// Simplifies prepared Postgres command usage by combining the prepared command
    /// and its usage into a single type.  It's also often useful to create derived
    /// custom types from this that handle the parameter definitions and subsitutions
    /// and perhaps precompute result column indexes to help abstract these details
    /// from the calling program.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You can use this class directly in your code like: 
    /// </para>
    /// <code language="c#">
    /// var parameters = new Dictionary&lt;string, NpgsqlDbType>()&gt; 
    ///     {
    ///         { "name", NpgsqlDbType.Text }
    ///     };
    ///     
    /// var preparedCommand = new PreparedCommand(connection, "SELECT Name, Age, Email FROM People WHERE Name = @name", parameters);
    /// 
    /// foreach (var name in new string[] { "jack", "jill", "john", "jane" })
    /// {
    ///     var queryCommand = preparedCommand.Clone();
    ///     
    ///     queryCommand.Parameters["name"].Value = name;
    ///     
    ///     foreach (var row in queryCommand.ExecuteReader().ToEnumerable())
    ///     {
    ///         Console.WriteLine($"Name: {row.GetString("Name")} Age: {row.GetInt32("Age"} Email: {row.GetString("Email")}");
    ///     }
    /// }
    /// </code>
    /// <para>
    /// In this example, we first created the prepared command that performs a query passing a 
    /// person's name as the parameter.   Note that we had to create a dictionary defining the 
    /// parameter name and type.  The below, we looped for perform four queries by cloning 
    /// the prepared command, setting the parameter values and then executing the command.
    /// </para>
    /// <para>
    /// Note how we used methods like <c>row.GetString("Name")</c> to access individual columns.
    /// This works and is convenient but will be somewhat inefficient because the method will need 
    /// to map the column name into the corresponding column index.  We could have specified 
    /// column indexes here, but that's starting to be fragile and could break if we inserted
    /// or removed result columns.  Even using names can be fragile since column names can
    /// be altered over time.
    /// </para>
    /// <para>
    /// We recommend writing custom classes that inherit from <see cref="PreparedCommand"/> to
    /// help abstract these things efficiently and without needing to use ORM frameworks like
    /// Entity Framework, NHibernate, and Dapper which tend to be inefficient and somewhat
    /// cumbersome to setup.  Here's an example of a class that wraps a prepared statement
    /// to implement the query from the example above:
    /// </para>
    /// <code language="c#">
    /// public class QueryPeopleByName : PreparedCommand
    /// {
    ///     private const string query = "SELECT Name, Age, Email FROM People WHERE Name = @name";
    ///     
    ///     public const int NameIndex  = 0;
    ///     public const int AgeIndex   = 1;
    ///     public const int EmailIndex = 2;
    ///     
    ///     private static readonly Dictionary&lt;string, NpgsqlDbType&gt; paramDefinitions =
    ///         new Dictionary&lt;string, NpgsqlDbType&gt;()
    ///         {
    ///             { "name", NpgsqlDbType.Text }
    ///         };
    ///     
    ///     public QueryPeopleByName(NpgsqlConnection connection)
    ///         : base(connection, query, paramDefinitions)
    ///     {
    ///         var queryCommand = queryPeopleByName.Clone();
    ///     
    ///         queryCommand.Parameters["name"] = name;
    ///         
    ///         return qiery
    ///     }
    ///     
    ///     public ReaderEnumerator GetPeople(string name)
    ///     {
    ///         var queryCommand = queryPeopleByName.Clone();
    ///     
    ///         queryCommand.Parameters["name"].Value = name;
    ///         
    ///         return queryCommand.ExecuteReader().ToEnumerable()
    ///     }
    /// }
    /// 
    /// ...
    /// 
    /// var queryPeopleByName = new QueryPeopleByName(connection);
    /// 
    /// foreach (var name in new string[] { "jack", "jill", "john", "jane" })
    /// {
    ///     foreach (var row in queryCommand.ExecuteReader().ToEnumerable())
    ///     {
    ///         Console.WriteLine($"Name: {row.GetString(QueryPeopleByName.NameIndex)} Age: {row.GetInt32(QueryPeopleByName.AgeIndex} Email: {row.GetString(QueryPeopleByName.EmailIndex)}");
    ///     }
    /// }
    /// </code>
    /// <para>
    /// The example above abstracted the query SQL, the parameter subsitution, as well as the result 
    /// column indexes to make this a little less fragile and easier to modify when necessary.  You can 
    /// extend this coding pattern by having your class handle conversion of the query result to nice
    /// .NET model objects:
    /// </para>
    /// <code language="c#">
    /// public class Person
    /// {
    ///     public string Name { get; set; }
    ///     public int Age { get; set; }
    ///     public string Email { get; set; }
    /// }
    /// 
    /// public class QueryPeopleByName : PreparedCommand
    /// {
    ///     private const string query = "SELECT Name, Age, Email FROM People WHERE Name = @name";
    ///     
    ///     private const int NameIndex  = 0;
    ///     private const int AgeIndex   = 1;
    ///     private const int EmailIndex = 2;
    ///     
    ///     private static readonly Dictionary&lt;string, NpgsqlDbType&gt; paramDefinitions =
    ///         new Dictionary&lt;string, NpgsqlDbType&gt;()
    ///         {
    ///             { "name", NpgsqlDbType.Text }
    ///         };
    ///     
    ///     public QueryPeopleByName(NpgsqlConnection connection)
    ///         : base(connection, query, paramDefinitions)
    ///     {
    ///         var queryCommand = queryPeopleByName.Clone();
    ///     
    ///         queryCommand.Parameters["name"] = name;
    ///         
    ///         return qiery
    ///     }
    ///     
    ///     public IEnumerable&lt;Person&gt; GetPeople(string name)
    ///     {
    ///         var queryCommand = queryPeopleByName.Clone();
    ///     
    ///         queryCommand.Parameters["name"] = name;
    ///         
    ///         foreach (var row in queryCommand.ExecuteReader().ToEnumerable())
    ///         {
    ///             yield return new Person()
    ///             {
    ///                 Name  = row.GetString(NameIndex),
    ///                 Age   = row.GetInt32(RowIndex),
    ///                 Email = row.GetString(EmailIndex);
    ///             };
    ///         }
    ///     }
    /// }
    /// 
    /// ...
    /// 
    /// var queryPeopleByName = new QueryPeopleByName(connection);
    /// 
    /// foreach (var name in new string[] { "jack", "jill", "john", "jane" })
    /// {
    ///     foreach (var person in queryCommand.GetProple(name))
    ///     {
    ///         Console.WriteLine($"Name: {person.Name} Age: {person.Age} Email: {person.Email}");
    ///     }
    /// }
    /// </code>
    /// <para>
    /// This final example abstracted the parameter name and type as well as converted
    /// the query result to compile-time <c>Person</c> object instances.  These patterns
    /// can provide a nice way to get some of the advantages of an ORM without extra
    /// runtime overhead.
    /// </para>
    /// </remarks>
    public class PreparedCommand
    {
        private NpgsqlCommand                       command;
        private Dictionary<string, NpgsqlDbType>    paramDefinitions;
        private bool                                isPrepared;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connection">The open Postgres connection.</param>
        /// <param name="sqlText">The command SQL.</param>
        /// <param name="paramDefinitions">
        /// <para>
        /// Optional parameter name and type definitions.
        /// </para>
        /// <note>
        /// Not all possible parameter types are supported by the common ones are at this time.
        /// </note>
        /// </param>
        /// <param name="prepareNow">
        /// Optionally specifies that the command is to be prepared immediately rather than
        /// waiting for it's first execution (the default).
        /// </param>
        public PreparedCommand(NpgsqlConnection connection, string sqlText, Dictionary<string, NpgsqlDbType> paramDefinitions = null, bool prepareNow = false)
        {
            Covenant.Requires<ArgumentNullException>(connection != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sqlText));

            this.command          = new NpgsqlCommand(sqlText, connection);
            this.paramDefinitions = paramDefinitions;

            if (paramDefinitions != null)
            {
                foreach (var item in paramDefinitions)
                {
                    command.Parameters.Add(item.Key, item.Value).Value = DBNull.Value;
                }
            }

            if (prepareNow)
            {
                command.Prepare();
                command.ExecuteNonQuery();

                this.isPrepared = true;
            }
            else
            {
                this.isPrepared = false;
            }
        }

        /// <summary>
        /// Prepares the underlying command if it hasn't already been prepared and
        /// then creates a clone of the command that can be executed after parameter
        /// values are set when necessary.
        /// </summary>
        /// <param name="transaction">Optional transaction.</param>
        public NpgsqlCommand Clone(NpgsqlTransaction transaction = null)
        {
            // Prepare the command if it hasn't been prepared yet.  Note that I'm
            // optimistically checking for [!isPrepared] to avoid the locking overhead
            // which will only be necessary on the first execution.

            if (!isPrepared)
            {
                lock (command)
                {
                    if (!isPrepared)
                    {
                        command.Prepare();
                        command.ExecuteNonQuery();

                        isPrepared = true;
                    }
                }
            }

            // Clone the statement.

            var clonedStatement = new NpgsqlCommand(command.CommandText, command.Connection, transaction);

            if (paramDefinitions != null)
            {
                foreach (var item in paramDefinitions)
                {
                    clonedStatement.Parameters.Add(item.Key, item.Value);
                }
            }

            clonedStatement.CommandTimeout = this.CommandTimeout;

            return clonedStatement;
        }

        /// <summary>
        /// Returns the command text.
        /// </summary>
        public string CommandText => command.CommandText;

        /// <summary>
        /// <para>
        /// Returns the command parameters.
        /// </para>
        /// <note>
        /// The collection returned should be considered tob <b>read-only</b> and
        /// must not be modified.
        /// </note>
        /// </summary>
        public NpgsqlParameterCollection Parameters => command.Parameters; 

        /// <summary>
        /// The command timeout in seconds.  This defaults to <b>30 seconds</b>.
        /// </summary>
        public int CommandTimeout { get; set; } = 30;
    }
}
