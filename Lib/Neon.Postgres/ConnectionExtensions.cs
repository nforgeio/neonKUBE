//-----------------------------------------------------------------------------
// FILE:	    ConnectionExtensions.cs
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
using System.Data;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

using Npgsql;

namespace Neon.Postgres
{
    /// <summary>
    /// Extends <see cref="NpgsqlConnection"/> with useful methods.
    /// </summary>
    public static class ConnectionExtensions
    {
        /// <summary>
        /// Clones an existing database connection by retaining all connection settings except that
        /// the new connection will be opened to target a new database.
        /// </summary>
        /// <param name="connection">The existing connection.</param>
        /// <param name="database">The target database for the new connection.</param>
        /// <returns>The new <see cref="NpgsqlConnection"/>.</returns>
        public static NpgsqlConnection OpenDatabase(this NpgsqlConnection connection, string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database), nameof(database));

            var targetConnection = connection.CloneWith($"host={connection.Host};port={connection.Port};database={database};user id={connection.UserName}");

            targetConnection.Open();

            return targetConnection;
        }

        /// <summary>
        /// Asynchronously clones an existing database connection by retaining all connection settings except
        /// that the connection will be opened to target a new database.
        /// </summary>
        /// <param name="connection">The existing connection.</param>
        /// <param name="database">The target database for the new connection.</param>
        /// <returns>The new <see cref="NpgsqlConnection"/>.</returns>
        public static async Task<NpgsqlConnection> OpenDatabaseAsync(this NpgsqlConnection connection, string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database), nameof(database));

            var targetConnection = connection.CloneWith($"host={connection.Host};port={connection.Port};database={database};user id={connection.UserName}");

            await targetConnection.OpenAsync();

            return targetConnection;
        }

        /// <summary>
        /// Executes a SQL command that does not perform a query.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="cmdText">The SQL command.</param>
        /// <param name="transaction">Optionally specifies the transaction.</param>
        /// <returns>The number of rows impacted.</returns>
        /// <remarks>
        /// <note>
        /// Although this method is convienent, consider explictly creating and
        /// preparing <see cref="NpgsqlCommand"/> for frequently executed commands
        /// for better performance.
        /// </note>
        /// </remarks>
        public static int ExecuteNonQuery(
            this NpgsqlConnection   connection,
            string                  cmdText, 
            NpgsqlTransaction       transaction = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cmdText), nameof(cmdText));

            NpgsqlCommand   command;

            if (transaction == null)
            {
                command = new NpgsqlCommand(cmdText, connection);
            }
            else
            {
                command = new NpgsqlCommand(cmdText, connection, transaction);
            }

            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Asynchronously executes a SQL command that does not perform a query.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="cmdText">The SQL command.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="transaction">Optionally specifies the transaction.</param>
        /// <returns>The number of rows impacted.</returns>
        /// <remarks>
        /// <note>
        /// Although this method is convienent, consider explictly creating and
        /// preparing <see cref="NpgsqlCommand"/> for frequently executed commands
        /// for better performance.
        /// </note>
        /// </remarks>
        public static async Task<int> ExecuteNonQueryAsync(
            this NpgsqlConnection   connection,
            string                  cmdText, 
            CancellationToken       cancellationToken = default,
            NpgsqlTransaction       transaction       = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cmdText), nameof(cmdText));

            NpgsqlCommand   command;

            if (transaction == null)
            {
                command = new NpgsqlCommand(cmdText, connection);
            }
            else
            {
                command = new NpgsqlCommand(cmdText, connection, transaction);
            }

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Executes a SQL query and returns the first column from the
        /// first row returned.  All other rows and columns will be ignored.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="cmdText">The SQL command.</param>
        /// <param name="transaction">Optionally specifies the transaction.</param>
        /// <returns>The value of the first column on the first row returned by the command.</returns>
        /// <remarks>
        /// <note>
        /// Although this method is convenient, consider explictly creating and
        /// preparing <see cref="NpgsqlCommand"/> for frequently executed commands
        /// for better performance.
        /// </note>
        /// </remarks>
        public static object ExecuteScalar(
            this NpgsqlConnection   connection,
            string                  cmdText,
            NpgsqlTransaction       transaction = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cmdText), nameof(cmdText));

            NpgsqlCommand command;

            if (transaction == null)
            {
                command = new NpgsqlCommand(cmdText, connection);
            }
            else
            {
                command = new NpgsqlCommand(cmdText, connection, transaction);
            }

            return command.ExecuteScalar();
        }

        /// <summary>
        /// Asynchronously executes a SQL query and returns the first column from the
        /// first row returned.  All other rows and columns will be ignored.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="cmdText">The SQL command.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="transaction">Optionally specifies the transaction.</param>
        /// <returns>The value of the first column on the first row returned by the command.</returns>
        /// <remarks>
        /// <note>
        /// Although this method is convenient, consider explictly creating and
        /// preparing <see cref="NpgsqlCommand"/> for frequently executed commands
        /// for better performance.
        /// </note>
        /// </remarks>
        public static async Task<object> ExecuteScalarAsync(
            this NpgsqlConnection   connection,
            string                  cmdText, 
            CancellationToken       cancellationToken = default,
            NpgsqlTransaction       transaction = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cmdText), nameof(cmdText));

            NpgsqlCommand command;

            if (transaction == null)
            {
                command = new NpgsqlCommand(cmdText, connection);
            }
            else
            {
                command = new NpgsqlCommand(cmdText, connection, transaction);
            }

            return await command.ExecuteScalarAsync(cancellationToken);
        }


        /// <summary>
        /// Executes a SQL query and returns the data reader to be used to process the results.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="cmdText">The SQL command.</param>
        /// <param name="behavior">Optionally specifies the command behavior.</param>
        /// <param name="transaction">Optionally specifies the transaction.</param>
        /// <returns>The <see cref="NpgsqlDataReader"/>.</returns>
        /// <remarks>
        /// <note>
        /// Although this method is convenient, consider explictly creating and
        /// preparing <see cref="NpgsqlCommand"/> for frequently executed commands
        /// for better performance.
        /// </note>
        /// </remarks>
        public static NpgsqlDataReader ExecuteReader(
            this NpgsqlConnection   connection,
            string                  cmdText,
            CommandBehavior         behavior    = CommandBehavior.Default,
            NpgsqlTransaction       transaction = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cmdText), nameof(cmdText));

            NpgsqlCommand   command;

            if (transaction == null)
            {
                command = new NpgsqlCommand(cmdText, connection);
            }
            else
            {
                command = new NpgsqlCommand(cmdText, connection, transaction);
            }

            return command.ExecuteReader(behavior);
        }

        /// <summary>
        /// Asynchronously executes a SQL query and returns the data reader to
        /// be used to process the results.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="cmdText">The SQL command.</param>
        /// <param name="behavior">Optionally specifies the command behavior.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="transaction">Optionally specifies the transaction.</param>
        /// <returns>The <see cref="NpgsqlDataReader"/>.</returns>
        /// <remarks>
        /// <note>
        /// Although this method is convenient, consider explictly creating and
        /// preparing <see cref="NpgsqlCommand"/> for frequently executed commands
        /// for better performance.
        /// </note>
        /// </remarks>
        public static async Task<NpgsqlDataReader> ExecuteReaderAsync(
            this NpgsqlConnection   connection,
            string                  cmdText, 
            CommandBehavior         behavior          = CommandBehavior.Default,
            CancellationToken       cancellationToken = default,
            NpgsqlTransaction       transaction       = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cmdText), nameof(cmdText));

            NpgsqlCommand   command;

            if (transaction == null)
            {
                command = new NpgsqlCommand(cmdText, connection);
            }
            else
            {
                command = new NpgsqlCommand(cmdText, connection, transaction);
            }

            return await command.ExecuteReaderAsync(behavior, cancellationToken);
        }

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
        /// Executes a batch of SQL commands saeparated by lines including <b>go</b>
        /// separators.  This works like Microsoft SQL server related tools.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="batchText">The SQL commands possibly separated by <b>go</b> lines.</param>
        /// <param name="transaction">Optionally specifies the transaction.</param>
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
            this NpgsqlConnection   connection,
            string                  batchText, 
            NpgsqlTransaction       transaction = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(batchText), nameof(batchText));

            foreach (var command in SplitBatch(batchText))
            {
                connection.ExecuteNonQuery(command, transaction);
            }
        }

        /// <summary>
        /// Asynchronously a batch of SQL commands saeparated by lines including <b>go</b>
        /// separators.  This works like Microsoft SQL server related tools.a
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="batchText">The SQL commands possibly separated by <b>go</b> lines.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="transaction">Optionally specifies the transaction.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
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
        public static async Task ExecuteBatchAsync(
            this NpgsqlConnection   connection,
            string                  batchText, 
            CancellationToken       cancellationToken = default,
            NpgsqlTransaction       transaction       = null)
        {
            foreach (var command in SplitBatch(batchText))
            {
                await connection.ExecuteNonQueryAsync(command, cancellationToken, transaction);
            }
        }
    }
}
