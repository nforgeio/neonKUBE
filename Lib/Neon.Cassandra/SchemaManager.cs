//-----------------------------------------------------------------------------
// FILE:	    SchemaManager.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Tasks;

using Cassandra;

namespace Neon.Cassandra
{
    /// <summary>
    /// Manages the initial creation and schema updates for a Cassandra keyspace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class uses some simple conventions to make it easy to upgrade a keyspace
    /// schema over time as the data model evolves.  This uses the concept of schema
    /// version numbers.  A schema version is simply an integer value where the version 
    /// will be <b>0</b> when a keyspace is initially created and then the version is
    /// incremented by one whenever the keyspace schema is updated.
    /// </para>
    /// <para>
    /// This class uses a reserved table named <see cref="DbInfoTableName"/> that is used to keep
    /// track of the current schema version.  This table will have a single row with these
    /// columns:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><b>Version:integer</b></term>
    ///         <description>
    ///         The integer keyspace schema version.  This will be set to <b>0</b> when
    ///         the keyspace is first created and will be incremented for each subsequent
    ///         update.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>Updater:text</b></term>
    ///         <description>
    ///         Used by multiple service instances to coordinate which one actually handles 
    ///         the update.  This will be `NULL` when the keyspace isn't being updated and
    ///         will be set to a string identifying the entity currently updating the keyspace.
    ///         This string can be anything from a GUID, container ID, hostname, or whatever.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>UpdateStartUtc:timestamp</b></term>
    ///         <description>
    ///         Time (UTC) when the most recent update was started.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>UpdateFinishUtc:timestamp</b></term>
    ///         <description>
    ///         Time (UTC) when the most recent update was completed.  This will be `NULL`
    ///         while an update is in progress.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// You'll be authoring Postgres SQL script files to create the initial keyspace 
    /// as well as to upgrade the keyspace for each subsequent schema change.  By convention,
    /// this class assumes that the SQL scripts for each keyspace will be saved to separate
    /// folders with each script file named like: <b>schema-#.script</b> where <b>#</b> is the
    /// schema version the script will upgrade the keyspace to, with <b>schema-0.script</b>
    /// being the script that creates the keyspace as <b>Version 0</b>.  So your script
    /// folder will look something like:
    /// </para>
    /// <code>
    /// schema-0000.script      &lt;-- keyspace created as v0
    /// schema-0001.script      &lt;-- upgrades from v0 to v1
    /// schema-0002.script      &lt;-- upgrades from v1 to v2
    /// schema-0003.script      &lt;-- upgrades from v2 to v3
    /// schema-0004.script      &lt;-- upgrades from v3 to v4
    /// ...
    /// schema-####.script
    /// </code>
    /// <note>
    /// This method parses the version number in the file names after the dash so it's perfectly
    /// OK to include leading zero digits there (like we did in the example above).  We actually
    /// recommend this so that your schema files can be sorted nicely by version when listed by
    /// the file system.
    /// </note>
    /// <para>
    /// Each script file is responsible for upgrading the keyspace from the previous version
    /// to the next.  This class will help manage the upgrade process by deciding which scripts
    /// need to be executed based on the <see cref="DbInfoTableName"/> table and then executing
    /// the required scripts.
    /// </para>
    /// <para>
    /// To use, construct an instance via <see cref="SchemaManager"/>, passing a keyspace session
    /// for the Postgres superuser or a user with the <b>CREATEDB</b> privilege.  You'll also need
    /// to pass the keyspace name and the path to the file system folder holding the script files.
    /// </para>
    /// <para>
    /// Then call <see cref="CreateKeyspaceAsync"/> to create the keyspace if it doesn't already
    /// exist; this uses the session passed to the constructor.  Then call 
    /// <see cref="UpgradeKeyspaceAsync(string, int, bool, Action{bool, int})"/> to apply
    /// any necessary updates; this sets the session to default to the target keyspace while
    /// the upgrade scripts are being executed.
    /// </para>
    /// <para>
    /// You may optionally pass a string to <see cref="UpgradeKeyspaceAsync(string, int, bool, Action{bool, int})"/>
    /// that identifies the entity performing the upgrade.  This could be an application name,
    /// the name of the host the updater is running on, the username of the person performing
    /// the upgrade etc.  This method uses this to try to prevent multiple updgrade from happening
    /// in parallel on the same keyspace (which would be bad) and the updater string can be used
    /// to help identify who else is updating the keyspace.  This parameter defaults to a GUID.
    /// </para>
    /// <para>
    /// Most applications will include at least two scripts when they get started with <b>schema-0.script</b>
    /// creating the keyspace and <b>schema-1.script</b> creating the tables, views, data types, 
    /// stored procedures, etc.
    /// </para>
    /// <para><b>SQL COMMAND BATCHES</b></para>
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
    /// But, this won't actually work because the keyspace generates a query plan
    /// for the entire command and when it does this and sees the inserts into
    /// [my_table] but the table doesn't actually exist at the time the query
    /// plan is being created.  So the command will fail.
    /// </para>
    /// <para>
    /// What you really need to do is create the table first as a separate
    /// command and then do the inserts as one or more subsequent commands.
    /// This is not terribly convenient so we've introduced the concept of
    /// a batch of commands.  Here's what this would look like:
    /// </para>
    /// <code language="sql">
    /// CREATE TABLE my_table (name text);
    /// go
    /// INSERT INTO my_table (name) values ('Jack');
    /// INSERT INTO my_table (name) values ('Jill');
    /// </code>
    /// <para>
    /// See how the <b>go</b> line separates the table creation from the inserts.
    /// This method will split the script files into separate commands on any <b>go</b> 
    /// lines and then execute these commands in order.
    /// </para>
    /// <note>
    /// <para>
    /// <b>go</b> is case insensitive and any leading or trailing space on the
    /// line will be ignored.
    /// </para>
    /// <para>
    /// Batch commands are implemented by <see cref="SessionExtensions.ExecuteBatch(ISession, string)"/>
    /// and an asynchonous alternative.
    /// </para>
    /// </note>
    /// <para><b>SCRIPT VARIABLES</b></para>
    /// <para>
    /// Your schema scripts may include variables of the form <b>${NAME}</b> where <b>NAME</b> is the
    /// case sensitive variable name.  The variable references will be replaced by the variable's
    /// value when the variable is defined, otherwise the variable reference will be left in place.
    /// </para>
    /// <para>
    /// The <b>${keyspace}</b> variable is reserved and will be replaced by the name of the Cassandra
    /// keyspace being managed. You can specify your own variables by passing a dictionary to the constructor.  
    /// This can be useful for specifying things like password, replication factors, etc.
    /// </para>
    /// <para><b>UPGRADE STRATEGIES</b></para>
    /// <para>
    /// The current implementation assumes that applications using the database are offline or can
    /// work properly with both the new and old schema.  Here are some siggestions for managing
    /// updates:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     Use YugaByte snapshots to backup the keyspace.
    ///     </item>
    ///     <item>
    ///     Effectively take the keyspace offline during the upgrade by revoking all rights
    ///     to all users besides the current one and the superuser before upgrading and then
    ///     restoring these rights afterwards.
    ///     </item>
    ///     <item>
    ///     For services and keyspaces deployed to Kubernetes, we recommend that you handle keyspace
    ///     schema updates via a custom Kubernetes operator which would stop any services using the
    ///     keyspace, apply the schema update, and then restart the services, potentially  upgrading 
    ///     them as well.  You could embed the schema scripts in the operator itself so upgrading the
    ///     keyspace (and application) would be as simple as upgrading the operator.
    ///     </item>
    /// </list>
    /// <para><b>HANDLING UPGRADE ERRORS</b></para>
    /// <para>
    /// It's possible for a database upgrade to fail.  Addressing upgrade failures will generally
    /// require manual intervention.  You should start out by looking at the <b>version and </b><b>error</b>
    /// columns in the <see cref="DbInfoTableName"/> in your database to diagnose what happened.
    /// <b>version</b> indicates the schema version before the update script was executed but that
    /// it's possible that the update script was paratially completed which means that the database
    /// may be in a state between the old and update schema version.
    /// </para>
    /// <para>
    /// Here are the underlying causes for upgrade errors:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><b>hardware/cluster</b></term>
    ///         <description>
    ///         <para>
    ///         The database cluster or the hardware/platform it's running is having problems
    ///         that prevent the updates from being applied.
    ///         </para>
    ///         <para>
    ///         The <b>error</b> column will describe the error.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>script errors</b></term>
    ///         <description>
    ///         <para>
    ///         Your upgrade scripts have syntax errors or are otherwise invalid.
    ///         </para>
    ///         <para>
    ///         The <b>error</b> column will describe the error.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>updater conflict</b></term>
    ///         <description>
    ///         <para>
    ///         Another updater is currently running or terminated for some reason 
    ///         before completing the update.
    ///         </para>
    ///         <para>
    ///         The <b>updater</b> column will identify the updater instance that is currently 
    ///         updating the database or that failed prematurely.
    ///         </para>
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// For <b>updater conflicts</b>, you'll need to determine whether the identified
    /// updater is still running or whether it has failed.  Simply wait for the other
    /// updater to finish if it's still running, otherwise you have a failure and will
    /// need to follow these recomendations to manually mitigate the situation:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><b>Manual Rollback</b></term>
    ///         <description>
    ///         It's possible that some but not all of the commands in your update script have 
    ///         completed.  Depending on the upgrade details, you may want to manually undo any 
    ///         of the statements that completed to get the database back to its state before
    ///         the the update started and then call <see cref="UpgradeKeyspaceAsync(string, int, bool, Action{bool, int})"/>
    ///         with <c>force: true</c>.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>Manual Upgrade</b></term>
    ///         <description>
    ///         As an alternative to <b>Manual Rollback</b>, you could simply execute the remaining
    ///         update commands manually and then updating the <see cref="DbInfoTableName"/> by setting
    ///         <b>version</b> to the new version number and setting the <b>updater</b> and <b>error</b>
    ///         fields to <c>NULL</c>.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>Script Corrections</b></term>
    ///         <description>
    ///         Be sure to correct any problems with your upgrade script, even if your are
    ///         going to manually complete the upgrade so that upgrades will work for new
    ///         database instances.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para><b>SCRIPTS AS EMBEDDED RESOURCES</b></para>
    /// <para>
    /// In addition to reading SQL scripts as standard files, the <see cref="SchemaManager"/> can
    /// also read scripts from embedded resources.  This is an easy and clean way to include these
    /// scripts in a program or library.  Here's what you need to do:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     Create a folder in your project to hold your SQL script files.
    ///     </item>
    ///     <item>
    ///     Add your scripts to the new folder, saving them with **UTF-8 encoding**.
    ///     </item>
    ///     <item>
    ///     Select your script files in the <b>Solution Explorer</b> and then left-click
    ///     on them and select **Properties**.  Set **Build Action** to **Embedded resource**.
    ///     </item>
    ///     <item>
    ///     You'll be using the <see cref="SchemaManager.SchemaManager(ISession, string, IStaticDirectory, Dictionary{string, string})"/>
    ///     override constructor and you'll be passing an <see cref="IStaticDirectory"/> that emulates a read-only file system
    ///     constructed from embedded resources.  You'll need to call <see cref="NeonAssemblyExtensions.GetResourceFileSystem(Assembly, string)"/>
    ///     to obtain this directory, passing a string identifying resource name prefix that identifies your virtual folder.
    ///     </item>
    /// </list>
    /// </remarks>
    public class SchemaManager : IDisposable
    {
        /// <summary>
        /// The name of the keyspace information table.
        /// </summary>
        public const string DbInfoTableName = "__dbinfo";

        private Dictionary<string, string>  variables = new Dictionary<string, string>();
        private ISession                    session;
        private string                      keyspace;
        private string                      scriptFolder;
        private Dictionary<int, string>     versionToScript;

        /// <summary>
        /// Constructs an instance that loads scripts from files.
        /// </summary>
        /// <param name="session">
        /// The master keyspace session to be used for creating the target keyspace.  This session must have been made 
        /// for a Postgres superuser or a user with global <b>CREATE</b> permission.
        /// </param>
        /// <param name="keyspace">The keyspace name to be used.</param>
        /// <param name="schemaFolder">The path to the file system folder holding the keyspace schema scripts.</param>
        /// <param name="variables">Optionally specifies script variables.</param>
        /// <exception cref="FileNotFoundException">
        /// Thrown if there's no directory at <see cref="scriptFolder"/> or when there's no
        /// <b>schema-0.script</b> file in the directory.
        /// </exception>
        public SchemaManager(ISession session, string keyspace, string schemaFolder, Dictionary<string, string> variables = null)
        {
            Covenant.Requires<ArgumentNullException>(session != null, nameof(session));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(keyspace), nameof(keyspace));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(schemaFolder), nameof(schemaFolder));

            this.session      = session;
            this.keyspace     = keyspace;
            this.scriptFolder = schemaFolder;

            // Initialize the variables dictionary.

            if (variables != null)
            {
                foreach (var item in variables)
                {
                    this.variables[item.Key] = item.Value;
                }
            }

            this.variables["keyspace"] = keyspace;

            // List the script files and load them into a dictionary keyed by the schema version
            // parsed from the file name.  We'll also check for duplicate schema files that differ
            // only by leading zeros in the name.

            var versionToScript = new Dictionary<int, string>();
            var scriptNameRegex = new Regex(@"schema-(?<version>\d+).script$");

            foreach (var scriptPath in Directory.GetFiles(schemaFolder, "*.script"))
            {
                var scriptName = Path.GetFileName(scriptPath);
                var match      = scriptNameRegex.Match(scriptName);

                if (!match.Success)
                {
                    throw new SchemaManagerException($"Unexpected script file [{scriptPath}].");
                }

                var scriptVersion = int.Parse(match.Groups["version"].Value);

                if (versionToScript.ContainsKey(scriptVersion))
                {
                    throw new SchemaManagerException($"There are multiple script files for schema [version={scriptVersion}].");
                }

                versionToScript.Add(scriptVersion, LoadScript(scriptPath));
            }

            if (!versionToScript.ContainsKey(0))
            {
                throw new FileNotFoundException($"[schema-0.script] keyspace creation script not found in: {Path.GetDirectoryName(schemaFolder)}");
            }

            this.versionToScript = versionToScript;
        }

        /// <summary>
        /// Constructs an instance that loads scripts from embedded resources.
        /// </summary>
        /// <param name="session">
        /// The master keyspace session to be used for creating the target keyspace.  This session must have been made 
        /// for a Postgres superuser or a user with global <b>CREATE</b> permission.
        /// </param>
        /// <param name="keyspace">The keyspace name to be used.</param>
        /// <param name="schemaDirectory">The embedded resource directory returned by a call to <see cref="NeonAssemblyExtensions.GetResourceFileSystem(Assembly, string)"/>.</param>
        /// <param name="variables">Optionally specifies script variables.</param>
        /// <exception cref="FileNotFoundException">
        /// Thrown if there's no directory at <see cref="scriptFolder"/> or when there's no
        /// <b>schema-0.script</b> file in the directory.
        /// </exception>
        public SchemaManager(ISession session, string keyspace, IStaticDirectory schemaDirectory, Dictionary<string, string> variables = null)
        {
            Covenant.Requires<ArgumentNullException>(session != null, nameof(session));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(keyspace), nameof(keyspace));
            Covenant.Requires<ArgumentNullException>(schemaDirectory != null, nameof(schemaDirectory));

            this.session      = session;
            this.keyspace     = keyspace;
            this.scriptFolder = schemaDirectory.Path;

            // Initialize the variables dictionary.

            if (variables != null)
            {
                foreach (var item in variables)
                {
                    this.variables[item.Key] = item.Value;
                }
            }

            this.variables["keyspace"] = keyspace;

            // List the script files and load them into a dictionary keyed by the schema version
            // parsed from the file name.  We'll also check for duplicate schema files that differ
            // only by leading zeros in the name.

            var versionToScript = new Dictionary<int, string>();
            var scriptNameRegex = new Regex(@"schema-(?<version>\d+).script$");

            foreach (var scriptFile in schemaDirectory.GetFiles("*.script"))
            {
                var scriptName = scriptFile.Name;
                var match      = scriptNameRegex.Match(scriptName);

                if (!match.Success)
                {
                    throw new SchemaManagerException($"Unexpected script file [{scriptFile}].");
                }

                var scriptVersion = int.Parse(match.Groups["version"].Value);

                if (versionToScript.ContainsKey(scriptVersion))
                {
                    throw new SchemaManagerException($"There are multiple script files for schema [version={scriptVersion}].");
                }

                versionToScript.Add(scriptVersion, LoadScript(scriptFile));
            }

            if (!versionToScript.ContainsKey(0))
            {
                throw new FileNotFoundException($"[schema-0.script] keyspace creation script not found in: {Path.GetDirectoryName(scriptFolder)}");
            }

            this.versionToScript = versionToScript;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~SchemaManager()
        {
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Handles the actual disposal.
        /// </summary>
        /// <param name="disposing"><b>true</b> if we're disposing, <c>false</c> for finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Reads the script text from a file path, replacing any variable references with the
        /// variable's value.
        /// </summary>
        /// <param name="scriptPath">The script file path.</param>
        /// <returns>The processed script text.</returns>
        private string LoadScript(string scriptPath)
        {
            using (var reader = new StreamReader(scriptPath))
            {
                using (var preprocessReader = new PreprocessReader(reader, variables))
                {
                    return preprocessReader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Reads the script text from an embedded resource file, replacing any variable references with the
        /// variable's value.
        /// </summary>
        /// <param name="scriptFile">The embedded resurce script file.</param>
        /// <returns>The processed script text.</returns>
        private string LoadScript(IStaticFile scriptFile)
        {
            using (var reader = scriptFile.OpenReader())
            {
                using (var preprocessReader = new PreprocessReader(reader, variables))
                {
                    return preprocessReader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Creates the keyspace using the <b>schema-0.script</b> file from the script folder.  This also
        /// creates the <see cref="DbInfoTableName"/> table adds a row setting the Version to 0.
        /// </summary>
        /// <returns><c>true</c> if the keyspace was created or <c>false</c> if it already exists.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the <b>schema-0.script</b> file does not exist in the script folder.</exception>
        /// <exception cref="SchemaManagerException">
        /// Thrown if the keyspace already exists but does not include the <see cref="DbInfoTableName"/>
        /// table or if that table doesn't have exactly one row or the version there is
        /// not positive.
        /// </exception>
        public async Task<bool> CreateKeyspaceAsync()
        {
            await SyncContext.ClearAsync;

            // Check to see if the keyspace already exists and if it exists, verify
            // that the DBINFO table exists and has a reasonable Version.

            var status = await GetStatusAsync();

            if (status.SchemaStatus == SchemaStatus.ExistsNoSchema)
            {
                throw new SchemaManagerException($"Keyspace [{keyspace}] exists but an invalid or missing [{DbInfoTableName}] table.");
            }

            if (status.SchemaStatus == SchemaStatus.ExistsWithSchema)
            {
                return false;
            }

            // We need to create the keyspace so read the [schema-0.script] script and execute it,
            // subsituting the keyspace name for any ${keyspace} macro variables.

            if (!status.VersionToScript.ContainsKey(0))
            {
                throw new SchemaManagerException($"No keyspace creation script file [schema-0.script] found in [{scriptFolder}].");
            }

            var orgKeyspace = session.Keyspace;

            try
            {
                var script = status.VersionToScript[0];

                script = script.Replace("${keyspace}", keyspace);

                await session.ExecuteBatchAsync(script);

                session.ChangeKeyspace(keyspace);

                // Add the DBINFO table and insert a row with set version=0.

                await session.ExecuteAsync($@"
CREATE TABLE IF NOT EXISTS {DbInfoTableName} (
    key                 integer,
    version             integer,
    updater             text,
    update_start_utc    timestamp,
    update_finish_utc   timestamp,
    error               text,
    PRIMARY KEY         (key)
);
");
                await session.ExecuteAsync($"INSERT INTO {DbInfoTableName}(key, version, updater, update_start_utc, update_finish_utc, error) VALUES(1, 0, NULL, currenttimestamp(), currenttimestamp(), NULL);");

                return true;
            }
            finally
            {
                session.ChangeKeyspace(orgKeyspace);
            }
        }

        /// <summary>
        /// Returns information about the keyspace schema status and schema scripts.
        /// </summary>
        /// <returns>The <see cref="KeyspaceStatus"/>.</returns>
        /// <exception cref="SchemaManagerException">Thrown when the keyspace has an invalid <see cref="DbInfoTableName"/> table.</exception>
        public async Task<KeyspaceStatus> GetStatusAsync()
        {
            await SyncContext.ClearAsync;

            var orgKeyspace = session.Keyspace;

            try
            {
                var status =
                    new KeyspaceStatus()
                    {
                        VersionToScript = versionToScript,
                        MaxVersion      = versionToScript.Keys.Max()
                    };

                // Ensure that the keyspace exists.

                var keyspaceExists = (await session.ExecuteAsync($"SELECT keyspace_name FROM system_schema.keyspaces where keyspace_name = '{keyspace}';")).HasRows();

                if (!keyspaceExists)
                {
                    status.SchemaStatus = SchemaStatus.NotFound;
                    return status;
                }

                session.ChangeKeyspace(keyspace);

                // Check to see that the DBINFO table exists.

                var dbInfoExists = (await session.ExecuteAsync($"SELECT table_name FROM system_schema.tables WHERE keyspace_name = '{keyspace}' AND table_name = '{DbInfoTableName}';")).HasRows();

                if (!dbInfoExists)
                {
                    status.SchemaStatus = SchemaStatus.ExistsNoSchema;
                    return status;
                }

                // Get the current schema version from the DBINFO table.

                var currentVersion = -1;
                var updater        = (string)null;
                var error          = (string)null;
                var rowCount       = 0;

                foreach (var row in await session.ExecuteAsync($"SELECT version, updater, update_start_utc, update_finish_utc, error FROM {DbInfoTableName};"))
                {
                    rowCount++;
                    
                    currentVersion = row.GetValue<int>("version");
                    updater        = row.GetValue<string>("updater");
                    error          = row.GetValue<string>("error");
                }

                if (rowCount != 1)
                {
                    throw new SchemaManagerException($"[{DbInfoTableName}] table has [{rowCount}] rows where only one row is expected.  This table may be corrupt.");
                }

                if (currentVersion < 0)
                {
                    throw new SchemaManagerException($"[{DbInfoTableName}.version={currentVersion}] is invalid.  Only positive version numbers are allowed.");
                }

                status.SchemaStatus = SchemaStatus.ExistsWithSchema;
                status.Version        = currentVersion;
                status.Updater        = updater;
                status.Error          = error;

                if (!string.IsNullOrEmpty(error))
                {
                    status.SchemaStatus = SchemaStatus.UpgradeError;
                }
                else if (!string.IsNullOrEmpty(updater))
                {
                    status.SchemaStatus = SchemaStatus.Updating;
                }

                return status;
            }
            finally
            {
                session.ChangeKeyspace(orgKeyspace);
            }
        }

        /// <summary>
        /// Upgrades the keyspace by applying any upgrade scripts from the current keyspace
        /// version to the latest update script found in the script folder or optionally when
        /// the keyspace version equals <paramref name="stopVersion"/>.
        /// </summary>
        /// <param name="updaterIdentity">
        /// <para>
        /// Optionally specifies the identity of the entity performing the update.  This may be the
        /// username of the person doing this or something identifying the service instance for
        /// more automated scenarios.  This service identity could be a hostname, container ID,
        /// or something else that makes sense.  This is used to ensure that only a single entity
        /// can update the keyspace.
        /// </para>
        /// <para>
        /// This defaults to a generated GUID.
        /// </para>
        /// </param>
        /// <param name="stopVersion">Optionally specifies the latest keyspace update to apply.</param>
        /// <param name="force">
        /// <para>
        /// Optionally specifies that any indication that another updater is in the process of
        /// updating the keyspace will be ignored and that any pewnding updates will proceed.
        /// This may be necessary after a previous update failed.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> You should take care to ensure that the other potential updater is
        /// not actually performing an update.  This may also means that the previous update
        /// was only partially completed which could require manual intervention.
        /// </note>
        /// </param>
        /// <param name="updateAction">
        /// Optional action that will be called before each update is applied and then afterwards.
        /// The <c>bool</c> argument will be <c>false</c> before the update is applied and <c>true</c>
        /// afterwards.  The <c>int</c> argument is the schema version being applied.
        /// </param>
        /// <returns>The version of the keyspace after the upgrade.</returns>
        /// <exception cref="SchemaManagerException">
        /// Thrown if the keyspace doesn't exist or does not include the
        /// <see cref="DbInfoTableName"/> table or if it invalid.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown if the <b>schema-0.script</b> file does not exist or when there are
        /// any missing script files (e.g. gaps in the sequence of files) or there
        /// are scripts with unexpected file names.
        /// </exception>
        /// <exception cref="SchemaManagerException">
        /// Thrown when another entity currently is in the process of updating the
        /// keyspace schema.
        /// </exception>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> This method does not perform the schema updates within a transaction
        /// because that will be impractical for large keyspaces and also due to limitations of
        /// YugaByte Postgres.  This means that you'll need to take care to ensure that your
        /// schema scripts are well tested and bulletproof and you should also consider backing
        /// up your keyspace to be very safe.
        /// </note>
        /// </remarks>
        public async Task<int> UpgradeKeyspaceAsync(string updaterIdentity = null, int stopVersion = -1, bool force = false, Action<bool, int> updateAction = null)
        {
            await SyncContext.ClearAsync;

            var orgKeyspace = session.Keyspace;

            try
            {
                session.ChangeKeyspace(keyspace);

                if (string.IsNullOrEmpty(updaterIdentity))
                {
                    updaterIdentity = Guid.NewGuid().ToString("d");
                }

                var status = await GetStatusAsync();

                if (status.SchemaStatus == SchemaStatus.NotFound)
                {
                    throw new SchemaManagerException($"Keyspace [{keyspace}] does not exist.");
                }
                else if (status.SchemaStatus == SchemaStatus.ExistsNoSchema)
                {
                    throw new SchemaManagerException($"Keyspace [{keyspace}] does not have a valid [{DbInfoTableName}] table.");
                }

                if (stopVersion >= 0)
                {
                    // If [stopVersion] was passed and the version equals the current schema version
                    // then there's nothing to do.

                    if (stopVersion == status.Version)
                    {
                        return stopVersion;
                    }

                    // Ensure that [stopVersion] is not less than the current version.

                    if (stopVersion < status.Version)
                    {
                        throw new SchemaManagerException($"[{nameof(stopVersion)}={stopVersion}] cannot be less than the current keyspace [version={status.Version}].");
                    }
                }

                // Ensure that [schema-0.script] exists and then verify that there are no gaps afterwards.

                var versionToScript = status.VersionToScript;
                var sortedScripts   = versionToScript.OrderBy(item => item.Key).ToArray();

                if (sortedScripts[0].Key != 0)
                {
                    throw new FileNotFoundException("Could not locate keyspace creation script file for schema version [0].");
                }

                for (int i = 0; i < sortedScripts.Length - 1; i++)
                {
                    if (sortedScripts[i + 1].Key != sortedScripts[i].Key + 1)
                    {
                        throw new FileNotFoundException($"Script file for schema [version={sortedScripts[i].Key + 1}] is missing.");
                    }
                }

                // IMPLEMENTATION NOTE:
                //
                // Attempt to gain exclusive keyspace access for the updater by setting the [Updater]
                // column in this updater's identity, throwing a [SchemaManagerException] if another
                // updater appears to be working on the database.
                //
                // Due to its noSQL/eventual-consistency nature, Cassandra cannot return the number of rows
                // actually updated by a query, so we can't rely on an atomic update to do this, so we'll do
                // a check first, followed by an update.  This means that there's a small chance that two 
                // updaters may begin updating the database in parallel.

                var updateInProgress = false;

                foreach (var row in (await session.ExecuteAsync($"SELECT updater FROM {DbInfoTableName} WHERE key = 1;")))
                {
                    if (row.GetValue<string>("updater") != null)
                    {
                        updateInProgress = true;
                        break;
                    }
                }

                if (updateInProgress && !force)
                {
                    throw new SchemaManagerException($"An update is already in progress for the [{keyspace}] keyspace.");
                }

                await session.ExecuteAsync($"UPDATE {DbInfoTableName} SET updater = '{updaterIdentity}', update_start_utc = currenttimestamp(), update_finish_utc = NULL WHERE key = 1;");

                // Apply any updates, incrementing the DBINFO table version afterwards as well as 
                // clearing the updater and setting the finish time after all of the updates have
                // been applied.

                var upgradeVersion = 0;

                try
                {
                    foreach (var item in sortedScripts)
                    {
                        upgradeVersion = item.Key;

                        if (item.Key <= status.Version)
                        {
                            continue;
                        }

                        if (updateAction != null)
                        {
                            updateAction.Invoke(false, item.Key);
                        }

                        var script = item.Value.Replace("${keyspace}", keyspace);

                        await session.ExecuteBatchAsync(script);
                        await session.ExecuteAsync($"UPDATE {DbInfoTableName} SET version = {item.Key} WHERE key = 1;");

                        if (updateAction != null)
                        {
                            updateAction.Invoke(true, item.Key);
                        }

                        if (stopVersion == item.Key)
                        {
                            break;
                        }
                    }

                    // Indicate that the update is complete.

                    await session.ExecuteAsync($"UPDATE {DbInfoTableName} SET updater = NULL, update_finish_utc = currenttimestamp() WHERE key = 1;");

                    return upgradeVersion;
                }
                catch (Exception e)
                {
                    // Record the error.

                    await session.ExecuteAsync(new SimpleStatement($"UPDATE {DbInfoTableName} SET error = ?, update_finish_utc = currenttimestamp() WHERE key = 1", e.Message));
                }

                return upgradeVersion;
            }
            finally
            {
                session.ChangeKeyspace(orgKeyspace);
            }
        }
    }
}
