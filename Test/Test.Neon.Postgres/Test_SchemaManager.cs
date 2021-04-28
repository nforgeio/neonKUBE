//-----------------------------------------------------------------------------
// FILE:	    Test_SchemaManager.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Postgres;
using Neon.Xunit;
using Neon.Xunit.YugaByte;

using Cassandra;
using Npgsql;

using Xunit;

namespace Test.Neon.Postgres
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_SchemaManager : IClassFixture<YugaByteFixture>
    {
        //---------------------------------------------------------------------
        // These need to be static because they maintain state across test runs.

        private static int          databaseId = 0;
        private static object       syncLock   = new object();

        //---------------------------------------------------------------------
        // Instance members.

        private NpgsqlConnection    postgres;

        public Test_SchemaManager(YugaByteFixture fixture)
        {
            // We're not going to restart YugaByte for every unit test because
            // that's too slow.  Instead, each test will work with unique database
            // names.

            fixture.Start();

            this.postgres = fixture.PostgresConnection;
        }

        /// <summary>
        /// Returns a unique database name for this test run.
        /// </summary>
        private string GetUniqueDatabaseName()
        {
            lock (syncLock)
            {
                return $"database_{databaseId++}";
            }
        }

        /// <summary>
        /// Creates a temporay folder and writes SQL scripts to files there.  The first script
        /// will be saved as <b>schema-1.script</b>, the second as <b>schema-2.script</b>, and so on.
        /// </summary>
        /// <param name="scripts">The script file contents.</param>
        /// <returns>The <see cref="TempFolder"/> holding the files.</returns>
        private async Task<TempFolder> PersistSchemaScriptsAsync(string[] scripts)
        {
            var tempFolder = new TempFolder();

            for (int i = 0; i < scripts.Length; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(tempFolder.Path, $"schema-{i}.script"), scripts[i]);
            }

            return tempFolder;
        }

        /// <summary>
        /// Creates a temporay folder and writes SQL scripts to files there.  The first script
        /// will be saved as <b>schema-0001.sql</b>, the second as <b>schema-0002.script</b>, and so on.
        /// </summary>
        /// <param name="scripts">The script file contents.</param>
        /// <returns>The <see cref="TempFolder"/> holding the files.</returns>
        private async Task<TempFolder> PersistSchemaScriptsWithZerosAsync(string[] scripts)
        {
            var tempFolder = new TempFolder();

            for (int i = 0; i < scripts.Length; i++)
            {
                var version = i.ToString("000#");

                await File.WriteAllTextAsync(Path.Combine(tempFolder.Path, $"schema-{version}.script"), scripts[i]);
            }

            return tempFolder;
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Constructor_NoScripts()
        {
            // Verify that we detect the situation where the script folder has no scripts.

            var databaseName = GetUniqueDatabaseName();

            using (var tempFolder = await PersistSchemaScriptsAsync(new string[0]))
            {
                Assert.Throws<FileNotFoundException>(
                    () =>
                    {
                        using (new SchemaManager(postgres, databaseName, tempFolder.Path))
                        {
                        }
                    });
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Constructor_NoCreateScript()
        {
            // Verify that we detect the situation where the script folder has some scripts
            // but no database creation script.

            var databaseName = GetUniqueDatabaseName();

            using (var tempFolder = new TempFolder())
            {
                await File.WriteAllTextAsync(Path.Combine(tempFolder.Path, "schema-1.script"), string.Empty);
                await File.WriteAllTextAsync(Path.Combine(tempFolder.Path, "schema-2.script"), string.Empty);

                Assert.Throws<FileNotFoundException>(
                    () =>
                    {
                        using (new SchemaManager(postgres, databaseName, tempFolder.Path))
                        {
                        }
                    });
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Create()
        {
            // Verify that the schema manager can create a database.

            var databaseName = GetUniqueDatabaseName();
            var scripts      = new string[]
            {
                "CREATE DATABASE ${database};"
            };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    Assert.True(await schemaManager.CreateDatabaseAsync());

                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(0, status.MaxVersion);
                    Assert.True(status.IsCurrent);
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Create_DatabaseExists()
        {
            // Verify that the schema manager database creation handles the case
            // where the database already exists and has a proper DBINFO table.

            var databaseName = GetUniqueDatabaseName();
            var scripts      = new string[]
            {
                "CREATE DATABASE ${database};"
            };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    Assert.True(await schemaManager.CreateDatabaseAsync());

                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(0, status.MaxVersion);
                    Assert.True(status.IsCurrent);

                    // CreateDatabaseAsync() should return FALSE here
                    // because that database already exists.

                    Assert.False(await schemaManager.CreateDatabaseAsync());

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(0, status.MaxVersion);
                    Assert.True(status.IsCurrent);
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Create_DatabaseExists_NoDBInfo()
        {
            // Verify that create throws an exception when the database already
            // exists but doesn't have a valid DBINFO table.

            var databaseName = GetUniqueDatabaseName();
            var scripts      = new string[]
            {
                "CREATE DATABASE ${database};"
            };

            await postgres.ExecuteNonQueryAsync($"CREATE DATABASE {databaseName};");

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsNoSchema, status.SchemaStatus);
                    Assert.Equal(-1, status.Version);
                    Assert.Equal(0, status.MaxVersion);
                    Assert.False(status.IsCurrent);

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.CreateDatabaseAsync());
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Update_MissingScript()
        {
            // Verify that we detect the situation where the script folder has a database
            // creation script but there's a version gap in the remaining scripts.

            var databaseName = GetUniqueDatabaseName();
            var scripts = 
                new string[]
                {
                    "CREATE DATABASE ${database};",
                    "CREATE my_table-0 (name text);",
                    "CREATE my_table-1 (name text);",
                    "CREATE my_table-2 (name text);"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                File.Delete(Path.Combine(tempFolder.Path, "schema-2.script"));

                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    await schemaManager.CreateDatabaseAsync();
                    await Assert.ThrowsAsync<FileNotFoundException>(async () => await schemaManager.UpgradeDatabaseAsync());
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Update_MissingDBInfo()
        {
            // Verify that update detects when the target database doesn't
            // have a DBINFO table.

            var databaseName = GetUniqueDatabaseName();
            var scripts      = new string[]
            {
                "CREATE DATABASE ${database};",
                "CREATE TABLE people (name text, age integer);"
            };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    Assert.True(await schemaManager.CreateDatabaseAsync());

                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(1, status.MaxVersion);
                    Assert.False(status.IsCurrent);

                    // Drop the DBINFO table for the test.

                    await schemaManager.TargetConnection.ExecuteNonQueryAsync($"DROP TABLE {SchemaManager.DbInfoTableName};");

                    status = await schemaManager.GetStatusAsync();
                    Assert.Equal(SchemaStatus.ExistsNoSchema, status.SchemaStatus);

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.UpgradeDatabaseAsync());
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Update_InvalidDBInfo()
        {
            // Verify that update detects when the target database has a DBINFO
            // table but that it's invalid.

            var databaseName = GetUniqueDatabaseName();
            var scripts      = new string[]
            {
                "CREATE DATABASE ${database};",
                "CREATE TABLE people (name text, age integer);"
            };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    Assert.True(await schemaManager.CreateDatabaseAsync());

                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(1, status.MaxVersion);
                    Assert.False(status.IsCurrent);

                    // Set a negative version in DBINFO and verify the exception.

                    await schemaManager.TargetConnection.ExecuteNonQueryAsync($"UPDATE {SchemaManager.DbInfoTableName} SET version = -1;");
                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.GetStatusAsync());
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Update_Required()
        {
            // Verify that update actually applies required updates.

            // Create the initial database and verify that it's up to date.

            var databaseName = GetUniqueDatabaseName();
            var scripts      =
                new string[]
                {
                    "CREATE DATABASE ${database};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    await schemaManager.CreateDatabaseAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal(4, await schemaManager.UpgradeDatabaseAsync());

                    // Verify that the updates were actually applied.

                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }

            // Add a couple additional upgrade scripts and verify.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                    "UPDATE my_table SET version = 5;",
                    "UPDATE my_table SET version = 6;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(4, status.Version);
                    Assert.Equal(6, status.MaxVersion);
                    Assert.Equal(6, await schemaManager.UpgradeDatabaseAsync());

                    // Verify that the updates were actually applied.

                    Assert.Equal(6, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Update_NotRequired()
        {
            // Verify that update does not apply updates that have already
            // been applied.

            var databaseName = GetUniqueDatabaseName();
            var scripts      =
                new string[]
                {
                    "CREATE DATABASE ${database};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    await schemaManager.CreateDatabaseAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal(4, await schemaManager.UpgradeDatabaseAsync());

                    // Verify that the updates were actually applied.

                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }

            // Add a modify the upgrade scripts to set differnt values and then 
            // verify that the scripts weren't executed again during an upgrade.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (101);",
                    "UPDATE my_table SET version = 102;",
                    "UPDATE my_table SET version = 103;",
                    "UPDATE my_table SET version = 104;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(4, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal(4, await schemaManager.UpgradeDatabaseAsync());

                    // Verify that the updates were not applied.

                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Update_Stop()
        {
            // Verify that we can stop updates at a specific version.

            var databaseName = GetUniqueDatabaseName();
            var scripts      =
                new string[]
                {
                    "CREATE DATABASE ${database};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    await schemaManager.CreateDatabaseAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);

                    // Verify that only the updates up to version=2 are applied.

                    Assert.Equal(2, await schemaManager.UpgradeDatabaseAsync(stopVersion: 2));

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(2, status.Version);
                    Assert.Equal(2, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));

                    // Apply the remaining updates and verify.

                    Assert.Equal(4, await schemaManager.UpgradeDatabaseAsync());

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Update_Stop_Error()
        {
            // Verify that we're not allowed to stop at a version lower
            // than the current database version.

            var databaseName = GetUniqueDatabaseName();
            var scripts      =
                new string[]
                {
                    "CREATE DATABASE ${database};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    await schemaManager.CreateDatabaseAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);

                    // Apply all of the updates.

                    Assert.Equal(4, await schemaManager.UpgradeDatabaseAsync());

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));

                    // Verify that we're not allowed to stop at an update that's
                    // already been applied.

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.UpgradeDatabaseAsync(stopVersion: 2));

                    // Verify that the database version hasn't changed.

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Scripts_WithLeadingZeros()
        {
            // Verify that we support script file names with leading zeros in
            // the version numbers.

            // Create the initial database and verify that it's up to date.

            var databaseName = GetUniqueDatabaseName();
            var scripts      =
                new string[]
                {
                    "CREATE DATABASE ${database};"
                };

            using (var tempFolder = await PersistSchemaScriptsWithZerosAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    await schemaManager.CreateDatabaseAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                };

            using (var tempFolder = await PersistSchemaScriptsWithZerosAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal(4, await schemaManager.UpgradeDatabaseAsync());

                    // Verify that the updates were actually applied.

                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }

            // Add a couple additional upgrade scripts and verify.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                    "UPDATE my_table SET version = 5;",
                    "UPDATE my_table SET version = 6;",
                };

            using (var tempFolder = await PersistSchemaScriptsWithZerosAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(4, status.Version);
                    Assert.Equal(6, status.MaxVersion);
                    Assert.Equal(6, await schemaManager.UpgradeDatabaseAsync());

                    // Verify that the updates were actually applied.

                    Assert.Equal(6, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Updater_Conflict()
        {
            // Verify that we can detect when another updater appears to be 
            // updating the database.

            var databaseName = GetUniqueDatabaseName();
            var scripts      =
                new string[]
                {
                    "CREATE DATABASE ${database};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    // Create the database.

                    await schemaManager.CreateDatabaseAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);

                    // Update the DBINFO table to make it appear that another updater is updating.

                    await schemaManager.TargetConnection.ExecuteNonQueryAsync($"UPDATE {SchemaManager.DbInfoTableName} SET updater = 'another-updater', update_start_utc = (now() at time zone 'utc'), update_finish_utc = NULL;");
                }
            }

            // Create a new schema manager, attempt an update and verify that
            // we detect the updater conflict.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.Updating, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);

                    // Attempt to apply the updates.  This should fail because another updater
                    // appears to be updating.

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.UpgradeDatabaseAsync());

                    // Try updating again with [force=true].  It should work this time.

                    Assert.Equal(4, await schemaManager.UpgradeDatabaseAsync(force: true));

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task Updater_Error()
        {
            // Verify that we can detect when another updater appears to be 
            // failed due to a simulated script execution error.

            var databaseName = GetUniqueDatabaseName();
            var scripts      =
                new string[]
                {
                    "CREATE DATABASE ${database};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    // Create the database.

                    await schemaManager.CreateDatabaseAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);

                    // Update the DBINFO table to make it appear that another updater failed.

                    await schemaManager.TargetConnection.ExecuteNonQueryAsync($"UPDATE {SchemaManager.DbInfoTableName} SET updater = 'another-updater', update_start_utc = (now() at time zone 'utc'), update_finish_utc = NULL, error = 'Something bad happened!';");
                }
            }

            // Create a new schema manager, attempt an update and verify that
            // we detect the updater conflict.

            scripts =
                new string[]
                {
                    "CREATE DATABASE ${database};",
@"CREATE TABLE my_table (version integer);
GO           
INSERT INTO my_table (version) values (1);",
                    "UPDATE my_table SET version = 2;",
                    "UPDATE my_table SET version = 3;",
                    "UPDATE my_table SET version = 4;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(postgres, databaseName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.UpgradeError, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal("Something bad happened!", status.Error);

                    // Attempt to apply the updates.  This should fail because another updater
                    // appears to be updating.

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.UpgradeDatabaseAsync());

                    // Try updating again with [force=true].  It should work this time.

                    Assert.Equal(4, await schemaManager.UpgradeDatabaseAsync(force: true));

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    Assert.Equal(4, (int)await schemaManager.TargetConnection.ExecuteScalarAsync("SELECT version FROM my_table;"));
                }
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonPostgres)]
        public async Task EmbeddedScripts()
        {
            // Verify that we can process scripts loaded from embedded resources.

            var databaseName = GetUniqueDatabaseName();

            using (var schemaManager = new SchemaManager(postgres, databaseName, Assembly.GetExecutingAssembly().GetResourceFileSystem("Test.Neon.Postgres.Scripts")))
            {
                await schemaManager.CreateDatabaseAsync();
                await schemaManager.UpgradeDatabaseAsync();
            }
        }
    }
}
