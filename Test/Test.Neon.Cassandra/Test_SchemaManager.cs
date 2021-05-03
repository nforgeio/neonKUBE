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

using Neon.Cassandra;
using Neon.Common;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.YugaByte;

using Cassandra;

using Xunit;

namespace Test.Neon.Cassandra
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_SchemaManager : IClassFixture<YugaByteFixture>
    {
        //---------------------------------------------------------------------
        // These need to be static because they maintain state across test runs.

        private static int          keyspaceId = 0;
        private static object       syncLock   = new object();

        //---------------------------------------------------------------------
        // Instance members.

        private ISession    cassandra;

        public Test_SchemaManager(YugaByteFixture fixture)
        {
            // We're not going to restart YugaByte for every unit test because
            // that's too slow.  Instead, each test will work with unique keyspace
            // names.

            fixture.Start();

            this.cassandra = fixture.CassandraSession;
        }

        /// <summary>
        /// Returns a unique keyspace name for this test run.
        /// </summary>
        private string GetUniqueKeyspaceName()
        {
            lock (syncLock)
            {
                return $"keyspace_{keyspaceId++}";
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
        /// will be saved as <b>schema-0001.script</b>, the second as <b>schema-0002.script</b>, and so on.
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

        /// <summary>
        /// Changes the current <see cref="cassandra"/> session's current keyspace to 
        /// a specified keyspace while execution an action.  The original keyspace will
        /// be restored before the method returns.
        /// </summary>
        /// <param name="tempKeyspace">The temporary keyspace.</param>
        /// <param name="action">The action.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteWithKeyspaceAsync(string tempKeyspace, Func<Task> action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tempKeyspace), nameof(tempKeyspace));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            var orgKeyspace = cassandra.Keyspace;

            cassandra.ChangeKeyspace(tempKeyspace);

            try
            {
                await action();
            }
            finally
            {
                cassandra.ChangeKeyspace(tempKeyspace);
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Constructor_NoScripts()
        {
            // Verify that we detect the situation where the script folder has no scripts.

            var keyspaceName = GetUniqueKeyspaceName();

            using (var tempFolder = await PersistSchemaScriptsAsync(new string[0]))
            {
                Assert.Throws<FileNotFoundException>(
                    () =>
                    {
                        using (new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                        {
                        }
                    });
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Constructor_NoCreateScript()
        {
            // Verify that we detect the situation where the script folder has some scripts
            // but no keyspace creation script.

            var keyspaceName = GetUniqueKeyspaceName();

            using (var tempFolder = new TempFolder())
            {
                await File.WriteAllTextAsync(Path.Combine(tempFolder.Path, "schema-1.script"), string.Empty);
                await File.WriteAllTextAsync(Path.Combine(tempFolder.Path, "schema-2.script"), string.Empty);

                Assert.Throws<FileNotFoundException>(
                    () =>
                    {
                        using (new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                        {
                        }
                    });
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Create()
        {
            // Verify that the schema manager can create a keyspace.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      = new string[]
            {
                "CREATE KEYSPACE ${keyspace};"
            };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    Assert.True(await schemaManager.CreateKeyspaceAsync());

                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(0, status.MaxVersion);
                    Assert.True(status.IsCurrent);
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Create_KeyspaceExists()
        {
            // Verify that the schema manager keyspace creation handles the case
            // where the keyspace already exists and has a proper DBINFO table.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      = new string[]
            {
                "CREATE KEYSPACE ${keyspace};"
            };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    Assert.True(await schemaManager.CreateKeyspaceAsync());

                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(0, status.MaxVersion);
                    Assert.True(status.IsCurrent);

                    // CreateKeyspaceAsync() should return FALSE here
                    // because that keyspace already exists.

                    Assert.False(await schemaManager.CreateKeyspaceAsync());

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(0, status.MaxVersion);
                    Assert.True(status.IsCurrent);
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Create_KeyspaceExists_NoDBInfo()
        {
            // Verify that create throws an exception when the keyspace already
            // exists but doesn't have a valid DBINFO table.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      = new string[]
            {
                "CREATE KEYSPACE ${keyspace};"
            };

            await cassandra.ExecuteAsync($"CREATE KEYSPACE {keyspaceName};");

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsNoSchema, status.SchemaStatus);
                    Assert.Equal(-1, status.Version);
                    Assert.Equal(0, status.MaxVersion);
                    Assert.False(status.IsCurrent);

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.CreateKeyspaceAsync());
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Update_MissingScript()
        {
            // Verify that we detect the situation where the script folder has a keyspace
            // creation script but there's a version gap in the remaining scripts.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts = 
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
                    "CREATE my_table-0 (name text);",
                    "CREATE my_table-1 (name text);",
                    "CREATE my_table-2 (name text);"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                File.Delete(Path.Combine(tempFolder.Path, "schema-2.script"));

                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    await schemaManager.CreateKeyspaceAsync();
                    await Assert.ThrowsAsync<FileNotFoundException>(async () => await schemaManager.UpgradeKeyspaceAsync());
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Update_MissingDBInfo()
        {
            // Verify that update detects when the target keyspace doesn't
            // have a DBINFO table.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      = new string[]
            {
                "CREATE KEYSPACE ${keyspace};",
                "CREATE TABLE people (name text, age integer);"
            };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    Assert.True(await schemaManager.CreateKeyspaceAsync());

                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(1, status.MaxVersion);
                    Assert.False(status.IsCurrent);

                    // Drop the DBINFO table for the test.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            await cassandra.ExecuteAsync($"DROP TABLE {SchemaManager.DbInfoTableName};");
                        });

                    status = await schemaManager.GetStatusAsync();
                    Assert.Equal(SchemaStatus.ExistsNoSchema, status.SchemaStatus);

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.UpgradeKeyspaceAsync());
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Update_InvalidDBInfo()
        {
            // Verify that update detects when the target keyspace has a DBINFO
            // table but that it's invalid.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      = new string[]
            {
                "CREATE KEYSPACE ${keyspace};",
                "CREATE TABLE people (name text, age integer);"
            };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    Assert.True(await schemaManager.CreateKeyspaceAsync());

                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(1, status.MaxVersion);
                    Assert.False(status.IsCurrent);

                    // Set a negative version in DBINFO and verify the exception.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            await cassandra.ExecuteAsync($"UPDATE {SchemaManager.DbInfoTableName} SET version = -1 WHERE key = 1;");
                        });
                    
                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.GetStatusAsync());
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Update_Required()
        {
            // Verify that update actually applies required updates.

            // Create the initial keyspace and verify that it's up to date.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    await schemaManager.CreateKeyspaceAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal(4, await schemaManager.UpgradeKeyspaceAsync());

                    // Verify that the updates were actually applied.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });
                }
            }

            // Add a couple additional upgrade scripts and verify.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                    "UPDATE my_table SET version = 5 WHERE key = 1;",
                    "UPDATE my_table SET version = 6 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(4, status.Version);
                    Assert.Equal(6, status.MaxVersion);
                    Assert.Equal(6, await schemaManager.UpgradeKeyspaceAsync());

                    // Verify that the updates were actually applied.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(6, row.GetValue<int>("version"));
                        });
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Update_NotRequired()
        {
            // Verify that update does not apply updates that have already
            // been applied.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    await schemaManager.CreateKeyspaceAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal(4, await schemaManager.UpgradeKeyspaceAsync());

                    // Verify that the updates were actually applied.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });
                }
            }

            // Add a modify the upgrade scripts to set differnt values and then 
            // verify that the scripts weren't executed again during an upgrade.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 101);",
                    "UPDATE my_table SET version = 102 WHERE key = 1;",
                    "UPDATE my_table SET version = 103 WHERE key = 1;",
                    "UPDATE my_table SET version = 104 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(4, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal(4, await schemaManager.UpgradeKeyspaceAsync());

                    // Verify that the updates were not applied.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Update_Stop()
        {
            // Verify that we can stop updates at a specific version.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    await schemaManager.CreateKeyspaceAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);

                    // Verify that only the updates up to version=2 are applied.

                    Assert.Equal(2, await schemaManager.UpgradeKeyspaceAsync(stopVersion: 2));

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(2, status.Version);
                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(2, row.GetValue<int>("version"));
                        });

                    // Apply the remaining updates and verify.

                    Assert.Equal(4, await schemaManager.UpgradeKeyspaceAsync());

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Update_Stop_Error()
        {
            // Verify that we're not allowed to stop at a version lower
            // than the current keyspace version.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    await schemaManager.CreateKeyspaceAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);

                    // Apply all of the updates.

                    Assert.Equal(4, await schemaManager.UpgradeKeyspaceAsync());

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });

                    // Verify that we're not allowed to stop at an update that's
                    // already been applied.

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.UpgradeKeyspaceAsync(stopVersion: 2));

                    // Verify that the keyspace version hasn't changed.

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Scripts_WithLeadingZeros()
        {
            // Verify that we support script file names with leading zeros in
            // the version numbers.

            // Create the initial keyspace and verify that it's up to date.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};"
                };

            using (var tempFolder = await PersistSchemaScriptsWithZerosAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    await schemaManager.CreateKeyspaceAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsWithZerosAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal(4, await schemaManager.UpgradeKeyspaceAsync());

                    // Verify that the updates were actually applied.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });
                }
            }

            // Add a couple additional upgrade scripts and verify.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                    "UPDATE my_table SET version = 5 WHERE key = 1;",
                    "UPDATE my_table SET version = 6 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsWithZerosAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.ExistsWithSchema, status.SchemaStatus);
                    Assert.Equal(4, status.Version);
                    Assert.Equal(6, status.MaxVersion);
                    Assert.Equal(6, await schemaManager.UpgradeKeyspaceAsync());

                    // Verify that the updates were actually applied.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(6, row.GetValue<int>("version"));
                        });
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Updater_Conflict()
        {
            // Verify that we can detect when another updater appears to be 
            // updating the keyspace.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    // Create the keyspace.

                    await schemaManager.CreateKeyspaceAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);

                    // Update the DBINFO table to make it appear that another updater is updating.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            await cassandra.ExecuteAsync($"UPDATE {SchemaManager.DbInfoTableName} SET updater = 'another-updater', update_start_utc = currenttimestamp(), update_finish_utc = NULL WHERE key = 1;");
                        });
                }
            }

            // Create a new schema manager, attempt an update and verify that
            // we detect the updater conflict.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.Updating, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);

                    // Attempt to apply the updates.  This should fail because another updater
                    // reported an error.

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.UpgradeKeyspaceAsync());

                    // Try updating again with [force=true].  It should work this time.

                    Assert.Equal(4, await schemaManager.UpgradeKeyspaceAsync(force: true));

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task Updater_Error()
        {
            // Verify that we can detect when another updater appears to be 
            // failed due to a simulated script execution error.

            var keyspaceName = GetUniqueKeyspaceName();
            var scripts      =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};"
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    // Create the keyspace.

                    await schemaManager.CreateKeyspaceAsync();

                    var status = await schemaManager.GetStatusAsync();

                    Assert.True(status.IsCurrent);

                    // Update the DBINFO table to make it appear that another updater failed.

                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            await cassandra.ExecuteAsync($"UPDATE {SchemaManager.DbInfoTableName} SET updater = 'another-updater', update_start_utc = currenttimestamp(), update_finish_utc = NULL, error = 'Something bad happened!' WHERE key = 1;");
                        });
                }
            }

            // Create a new schema manager with upgrade scripts and ensure that 
            // the updates are applied.

            scripts =
                new string[]
                {
                    "CREATE KEYSPACE ${keyspace};",
@"CREATE TABLE my_table (key integer, version integer, PRIMARY KEY (key));
GO           
INSERT INTO my_table (key, version) values (1, 1);",
                    "UPDATE my_table SET version = 2 WHERE key = 1;",
                    "UPDATE my_table SET version = 3 WHERE key = 1;",
                    "UPDATE my_table SET version = 4 WHERE key = 1;",
                };

            using (var tempFolder = await PersistSchemaScriptsAsync(scripts))
            {
                using (var schemaManager = new SchemaManager(cassandra, keyspaceName, tempFolder.Path))
                {
                    var status = await schemaManager.GetStatusAsync();

                    Assert.Equal(SchemaStatus.UpgradeError, status.SchemaStatus);
                    Assert.Equal(0, status.Version);
                    Assert.Equal(4, status.MaxVersion);
                    Assert.Equal("Something bad happened!", status.Error);

                    // Attempt to apply the updates.  This should fail because another updater
                    // appears to be updating.

                    await Assert.ThrowsAsync<SchemaManagerException>(async () => await schemaManager.UpgradeKeyspaceAsync());

                    // Try updating again with [force=true].  It should work this time.

                    Assert.Equal(4, await schemaManager.UpgradeKeyspaceAsync(force: true));

                    status = await schemaManager.GetStatusAsync();

                    Assert.Equal(4, status.Version);
                    await ExecuteWithKeyspaceAsync(keyspaceName,
                        async () =>
                        {
                            var row = (await cassandra.ExecuteAsync("SELECT version FROM my_table WHERE key = 1;")).Single();

                            Assert.Equal(4, row.GetValue<int>("version"));
                        });
                }
            }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCassandra)]
        public async Task EmbeddedScripts()
        {
            // Verify that we can process scripts loaded from embedded resources.

            var keyspaceName = GetUniqueKeyspaceName();

            using (var schemaManager = new SchemaManager(cassandra, keyspaceName, Assembly.GetExecutingAssembly().GetResourceFileSystem("Test.Neon.Cassandra.Scripts")))
            {
                await schemaManager.CreateKeyspaceAsync();
                await schemaManager.UpgradeKeyspaceAsync();
            }
        }
    }
}
