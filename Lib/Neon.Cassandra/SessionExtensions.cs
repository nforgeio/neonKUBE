//-----------------------------------------------------------------------------
// FILE:	    SessionExtensions.cs
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
    /// Extends <see cref="ISession"/> with useful methods.
    /// </summary>
    public static class SessionExtensions
    {
        /// <summary>
        /// Splits a batch of SQL commands potentially separated by <b>go</b> lines
        /// into the distinct commands.
        /// </summary>
        /// <param name="batchText">The command batch.</param>
        /// <returns>The list of SQL commands from the batch.</returns>
        private static List<string> SplitBatch(string batchText)
        {
            var commands = new List<string>();
            var command  = string.Empty;
            var sb       = new  StringBuilder();

            using (var reader = new StringReader(batchText))
            {
                foreach (var line in reader.Lines())
                {
                    var trimmed = line.Trim();

                    if (trimmed.Equals("go", StringComparison.InvariantCultureIgnoreCase))
                    {
                        command = sb.ToString();

                        if (!string.IsNullOrWhiteSpace(command))
                        {
                            commands.Add(command);
                        }

                        sb.Clear();
                    }
                    else
                    {
                        sb.AppendLine(line);
                    }
                }
            }

            // Add any trailing command not terminated by a GO line.

            command = sb.ToString();

            if (!string.IsNullOrWhiteSpace(command))
            {
                commands.Add(command);
            }

            return commands;
        }

        /// <summary>
        /// <para>
        /// Executes a batch of SQL commands saeparated by lines including <b>go</b>
        /// separators.  This works like Microsoft SQL server related tools.
        /// </para>
        /// <note>
        /// <para>
        /// The term <i>batch</i> here is different from the usual Cassandra terminology,
        /// where batch refers an <see cref="BatchStatement"/> which may include multiple
        /// statements that are executed together atomically.  Batch here refers to statements
        /// extracted from the text passed and then executed <b>individually</b>.
        /// </para>
        /// <para>
        /// Sorry for the confusion here, but we used this to be consistent with the our
        /// Postgres extensions and frankly, we couldn't think of a better term.
        /// </para>
        /// </note>
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="batchText">The SQL commands possibly separated by <b>go</b> lines.</param>
        /// <remarks>
        /// <para>
        /// It's often necessary to execute a sequence of SQL commands that depend on
        /// each other.  One example is a command that creates a table followed by 
        /// commands that write rows.  You might think that you could achieve this
        /// by executing the following as one command:
        /// </para>
        /// <code language="sql">
        /// CREATE TABLE my_table (name text);
        /// INSERT INTO my_table (name) values ('Jack');
        /// INSERT INTO my_table (name) values ('Jill');
        /// </code>
        /// <para>
        /// but this won't actually work because the database generates a query plan
        /// for the entire command and when it does this and sees the inserts into
        /// [my_table] but the table doesn't actually exist at the time the query
        /// plan is being created.  So the command will fail.
        /// </para>
        /// <para>
        /// What you really need to do is create the table first as a separate
        /// command and then do the inserts as one or more subsequent commands.
        /// This is not terribly convenient so we've introduced the concept of
        /// a batch of commands via this method.  Here's what this would look like:
        /// </para>
        /// <code language="sql">
        /// CREATE TABLE my_table (name text);
        /// go
        /// INSERT INTO my_table (name) values ('Jack');
        /// INSERT INTO my_table (name) values ('Jill');
        /// </code>
        /// <para>
        /// See how the <b>go</b> line separates the table creation from the inserts.
        /// This method will split the <paramref name="batchText"/> into separate
        /// commands on any <b>go</b> lines and then execute these commands in order.
        /// </para>
        /// <note>
        /// <b>go</b> is case insensitive and any leading or trailing space on the
        /// line will be ignored.
        /// </note>
        /// </remarks>
        public static void ExecuteBatch(
            this ISession       session,
            string              batchText)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(batchText), nameof(batchText));

            foreach (var command in SplitBatch(batchText))
            {
                session.Execute(command);
            }
        }

        /// <summary>
        /// <para>
        /// Asynchronously a batch of SQL commands saeparated by lines including <b>go</b>
        /// separators.  This works like Microsoft SQL server related tools.
        /// </para>
        /// <note>
        /// <para>
        /// The term <i>batch</i> here is different from the usual Cassandra terminology,
        /// where batch refers an <see cref="BatchStatement"/> which may include multiple
        /// statements that are executed together atomically.  Batch here refers to statements
        /// extracted from the text passed and then executed <b>individually</b>.
        /// </para>
        /// <para>
        /// Sorry for the confusion here, but we used this to be consistent with the our
        /// Postgres extensions and frankly, we couldn't think of a better term.
        /// </para>
        /// </note>
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="batchText">The SQL commands possibly separated by <b>go</b> lines.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method doesn't actually execute the statements asynchronously because
        /// the Cassandra driver doesn't include an <b>ISession.ExecuteAsync(string)</b>
        /// method.  This method simply calls <see cref="ExecuteBatch(ISession, string)"/>.
        /// We're retaining this method for compatibility with our Postgres extensions.
        /// </note>
        /// <para>
        /// It's often necessary to execute a sequence of SQL commands that depend on
        /// each other.  One example is a command that creates a table followed by 
        /// commands that write rows.  You might think that you could achieve this
        /// by executing the following as one command:
        /// </para>
        /// <code language="sql">
        /// CREATE TABLE my_table (name text);
        /// INSERT INTO my_table (name) values ('Jack');
        /// INSERT INTO my_table (name) values ('Jill');
        /// </code>
        /// <para>
        /// but this won't actually work because the database generates a query plan
        /// for the entire command and when it does this and sees the inserts into
        /// [my_table] but the table doesn't actually exist at the time the query
        /// plan is being created.  So the command will fail.
        /// </para>
        /// <para>
        /// What you really need to do is create the table first as a separate
        /// command and then do the inserts as one or more subsequent commands.
        /// This is not terribly convenient so we've introduced the concept of
        /// a batch of commands via this method.  Here's what this would look like:
        /// </para>
        /// <code language="sql">
        /// CREATE TABLE my_table (name text);
        /// go
        /// INSERT INTO my_table (name) values ('Jack');
        /// INSERT INTO my_table (name) values ('Jill');
        /// </code>
        /// <para>
        /// See how the <b>go</b> line separates the table creation from the inserts.
        /// This method will split the <paramref name="batchText"/> into separate
        /// commands on any <b>go</b> lines and then execute these commands in order.
        /// </para>
        /// <note>
        /// <b>go</b> is case insensitive and any leading or trailing space on the
        /// line will be ignored.
        /// </note>
        /// </remarks>
        public async static Task ExecuteBatchAsync(
            this ISession       session,
            string              batchText)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(batchText), nameof(batchText));

            foreach (var command in SplitBatch(batchText))
            {
                await session.ExecuteAsync(command);
            }
        }

        /// <summary>
        /// Executes a text command asynchronously.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="cqlText">The command or query text.</param>
        /// <returns>The resulting <see cref="RowSet"/>.</returns>
        public async static Task<RowSet> ExecuteAsync(
            this ISession       session,
            string              cqlText)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cqlText), nameof(cqlText));

            return await session.ExecuteAsync(new SimpleStatement(cqlText));
        }
    }
}
