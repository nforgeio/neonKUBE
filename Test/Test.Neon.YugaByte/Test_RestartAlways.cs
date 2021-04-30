//-----------------------------------------------------------------------------
// FILE:	    Test_RestartAlways.cs
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.YugaByte;

using Cassandra;
using Npgsql;

using Xunit;

namespace TestYugaByte
{
    /// <summary>
    /// These tests verify that we can connect to via Cassandra and Postgres and also
    /// that <see cref="YugaByteFixture.Restart()"/> actually clears the database.
    /// </summary>
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_RestartAlways : IClassFixture<YugaByteFixture>
    {
        private YugaByteFixture     fixture;
        private ISession            cassandra;
        private string              cassandraKeyspace;
        private NpgsqlConnection    postgres;
        private string              postgresDatabase;

        public Test_RestartAlways(YugaByteFixture fixture)
        {
            if (fixture.Start() == TestFixtureStatus.AlreadyRunning)
            {
                fixture.Restart();
            }

            this.fixture           = fixture;
            this.cassandra         = fixture.CassandraSession;
            this.cassandraKeyspace = fixture.CassandraSession.Keyspace;
            this.postgres          = fixture.PostgresConnection;
            this.postgresDatabase  = fixture.PostgresConnection.Database;
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonYugaByte)]
        public void Cassandra0()
        {
            cassandra.Execute("CREATE TABLE employee(id int PRIMARY KEY, name varchar, age int, language varchar)");
            cassandra.Execute("INSERT INTO employee(id, name, age, language) VALUES (1, 'John', 35, 'C#')");
            Assert.Single(cassandra.Execute("SELECT * from employee"));
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonYugaByte)]
        public void Cassandra1()
        {
            cassandra.Execute("CREATE TABLE employee(id int PRIMARY KEY, name varchar, age int, language varchar)");
            cassandra.Execute("INSERT INTO employee(id, name, age, language) VALUES (1, 'John', 35, 'C#')");
            Assert.Single(cassandra.Execute("SELECT * from employee"));
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonYugaByte)]
        public void Postgres0()
        {
            NpgsqlCommand command;

            command = new NpgsqlCommand($"CREATE TABLE employee (id int PRIMARY KEY, name varchar, age int, language varchar);", postgres);
            command.ExecuteNonQuery();

            command = new NpgsqlCommand("INSERT INTO employee (id, name, age, language) VALUES (1, 'John', 35, 'CSharp');", postgres);
            command.ExecuteNonQuery();

            var query = new NpgsqlCommand("SELECT COUNT(*) FROM employee", postgres);
            Assert.Equal(1L, query.ExecuteScalar());
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonYugaByte)]
        public void Postgres1()
        {
            NpgsqlCommand command;

            command = new NpgsqlCommand($"CREATE TABLE employee (id int PRIMARY KEY, name varchar, age int, language varchar);", postgres);
            command.ExecuteNonQuery();

            command = new NpgsqlCommand("INSERT INTO employee (id, name, age, language) VALUES (1, 'John', 35, 'CSharp');", postgres);
            command.ExecuteNonQuery();

            var query = new NpgsqlCommand("SELECT COUNT(*) FROM employee", postgres);
            Assert.Equal(1L, query.ExecuteScalar());
        }
    }
}
