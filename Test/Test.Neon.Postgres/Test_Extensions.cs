//-----------------------------------------------------------------------------
// FILE:	    Test_Extensions.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Postgres;
using Neon.Xunit;
using Neon.Xunit.YugaByte;

using Cassandra;
using Npgsql;
using NpgsqlTypes;

using Xunit;

namespace Test.Neon.Postgres
{
    [Trait(TestTrait.Category, TestArea.NeonPostgres)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_Extensions : IClassFixture<YugaByteFixture>
    {
        private NpgsqlConnection postgres;

        public Test_Extensions(YugaByteFixture fixture)
        {
            TestHelper.ResetDocker(this.GetType());

            // We're not going to initialize the Postgres once and then have
            // all of the tests workagainst that, rather than recreating the
            // database every time (which would be slow).

            var status = fixture.Start();

            this.postgres = fixture.PostgresConnection;

            if (status == TestFixtureStatus.Started)
            {
                var sbSetupScript =
@"
CREATE TABLE enumerate_table (
    value   integer
);
go

INSERT INTO enumerate_table (value) values (0);
INSERT INTO enumerate_table (value) values (1);
INSERT INTO enumerate_table (value) values (2);
INSERT INTO enumerate_table (value) values (3);
INSERT INTO enumerate_table (value) values (4);
INSERT INTO enumerate_table (value) values (5);
INSERT INTO enumerate_table (value) values (6);
INSERT INTO enumerate_table (value) values (7);
INSERT INTO enumerate_table (value) values (8);
INSERT INTO enumerate_table (value) values (9);
";
                postgres.ExecuteBatch(sbSetupScript);
            }
        }

        [Fact]
        public void Enumerate()
        {
            var values = new HashSet<int>();

            using (var reader = postgres.ExecuteReader("SELECT value FROM enumerate_table;"))
            {
                foreach (var row in reader.ToEnumerable())
                {
                    values.Add(row.GetInt32(0));
                }
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.Contains(i, values);
            }
        }

        [Fact]
        public async Task EnumerateAsync()
        {
            var values = new HashSet<int>();

            using (var reader = await postgres.ExecuteReaderAsync("SELECT value FROM enumerate_table;"))
            {
                await foreach (var row in reader.ToAsyncEnumerable())
                {
                    values.Add(row.GetInt32(0));
                }
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.Contains(i, values);
            }
        }

        [Fact]
        public async Task PrepareCommand_NoArg()
        {
            var preparedCommand = new PreparedCommand(postgres, "SELECT value FROM enumerate_table WHERE value = @value;");
            var command         = preparedCommand.Clone();
            var values          = new HashSet<int>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                await foreach (var row in reader.ToAsyncEnumerable())
                {
                    values.Add(row.GetInt32(0));
                }
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.Contains(i, values);
            }
        }

        [Fact]
        public async Task PrepareCommand_Arg()
        {
            var singleParameters = new Dictionary<string, NpgsqlDbType>()
            {
                { "Value", NpgsqlDbType.Integer }
            };

            var preparedCommand = new PreparedCommand(postgres, "SELECT value FROM enumerate_table WHERE value = @value;", singleParameters);
            var command         = preparedCommand.Clone();
            var values          = new HashSet<int>();

            command.Parameters["value"].Value = 5;

            using (var reader = await command.ExecuteReaderAsync())
            {
                await foreach (var row in reader.ToAsyncEnumerable())
                {
                    values.Add(row.GetInt32(0));
                }
            }

            Assert.Single(values);
            Assert.Contains(values, v => v == 5);
        }
    }
}
