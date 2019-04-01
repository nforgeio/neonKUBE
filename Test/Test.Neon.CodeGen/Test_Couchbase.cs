//-----------------------------------------------------------------------------
// FILE:	    Test_Couchbase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

using Test.Models;

namespace TestCodeGen.Couchbase
{
    public class PlainOldObject
    {
        public string Foo { get; set; }
    }

    public class Test_Couchbase : IClassFixture<CouchbaseFixture>
    {
        private const string username = "Administrator";
        private const string password = "password";

        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_Couchbase(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            couchbase.Start();

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public async Task WriteReadList()
        {
            // Verify that we can write generated entity models.

            var jackEntity = new PersonEntity()
            {
                Id = 0,
                Name = "Jack",
                Age = 10,
                Data = new byte[] { 0, 1, 2, 3, 4 }
            };

            var jillEntity = new PersonEntity()
            {
                Id = 1,
                Name = "Jill",
                Age = 11,
                Data = new byte[] { 5, 6, 7, 8, 9 }
            };

            Assert.Equal("0", jackEntity.GetKey());
            Assert.Equal("1", jillEntity.GetKey());

            await bucket.InsertSafeAsync(jackEntity.GetKey(), jackEntity, persistTo: PersistTo.One);
            await bucket.InsertSafeAsync(jillEntity.GetKey(), jillEntity, persistTo: PersistTo.One);

            // Verify that we can read them.

            var jackReadEntity = await bucket.GetSafeAsync<PersonEntity>(0.ToString());
            var jillReadEntity = await bucket.GetSafeAsync<PersonEntity>(1.ToString());

            Assert.Equal("0", jackReadEntity.GetKey());
            Assert.Equal("1", jillReadEntity.GetKey());
            Assert.True(jackEntity == jackReadEntity);
            Assert.True(jillEntity == jillReadEntity);

            //-----------------------------------------------------------------
            // Persist a [City] entity (which has a different entity type) and then
            // perform a N1QL query to list the Person entities and verify that we
            // get only Jack and Jill back.  This verifies the the [TypeFilter] 
            // attribute is generated and working correctly.

            var cityEntity = new CityEntity()
            {
                Name = "Woodinville",
                Population = 12345
            };

            var opResult = await bucket.InsertSafeAsync(cityEntity.GetKey(), cityEntity, persistTo: PersistTo.One);

            var context     = new BucketContext(bucket);
            var peopleQuery = from doc in context.Query<PersonEntity>() select doc;

            // $todo(jeff.lill):
            //
            // I need to figure out how to have the query honor the mutation state
            // returned in [opResult].  I'm going to hack a delay here in the meantime.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/473

            await Task.Delay(TimeSpan.FromSeconds(2));

            var people = peopleQuery.ToList();

            Assert.Equal(2, people.Count);
            Assert.Contains(people, p => p.Name == "Jack");
            Assert.Contains(people, p => p.Name == "Jill");

            //-----------------------------------------------------------------
            // Query for the city and verify.

            var cityQuery = from doc in context.Query<CityEntity>() select doc;
            var cities    = cityQuery.ToList();

            Assert.Single(cities);
            Assert.Contains(cities, p => p.Name == "Woodinville");

            //-----------------------------------------------------------------
            // Verify that plain old object serialization still works.

            var poo = new PlainOldObject() { Foo = "bar" };

            await bucket.InsertSafeAsync("poo", poo, persistTo: PersistTo.One);

            poo = await bucket.GetSafeAsync<PlainOldObject>("poo");

            Assert.Equal("bar", poo.Foo);

            //-----------------------------------------------------------------
            // Extra credit #1: Verify that [Entity.ToBase()] works.

            var jack = jackEntity.ToBase();

            Assert.Equal(jackEntity.Name, jack.Name);
            Assert.Equal(jackEntity.Age, jack.Age);

            // The underlying [JObject] shouldn't have any properties
            // with leading underscores because ToBase() should have
            // stripped the [__EntityType] property off.

            foreach (var property in jack.ToJObject(noClone: true).Properties())
            {
                Assert.False(property.Name.StartsWith("__"));
            }

            //-----------------------------------------------------------------
            // Extra credit #2: Verify that [Entity.DeepClone()] works.

            var clone = jackEntity.DeepClone();

            Assert.Equal(jackEntity.Name, clone.Name);
            Assert.Equal(jackEntity.Age, clone.Age);
            Assert.NotSame(jackEntity.Data, clone.Data);
        }
    }
}
