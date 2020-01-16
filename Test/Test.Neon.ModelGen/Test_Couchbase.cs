//-----------------------------------------------------------------------------
// FILE:	    Test_Couchbase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;

using Neon.ModelGen;
using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

using Test.Neon.Models;

namespace TestModelGen.Couchbase
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
        private BucketContext       context;

        public Test_Couchbase(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            if (couchbase.Start() == TestFixtureStatus.AlreadyRunning)
            {
                couchbase.Clear();
            }

            bucket  = couchbase.Bucket;
            context = new BucketContext(bucket);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task WriteReadList()
        {
            // Ensure that the database starts out empty.

            Assert.Empty(from doc in context.Query<object>() select doc);

            // Verify that the generated CreateKey() methods return the
            // correct values.

            Assert.Equal($"{Person.PersistedType}::0", Person.CreateKey("0"));
            Assert.Equal($"{City.PersistedType}::Woodinville", City.CreateKey("Woodinville"));

            // Verify that we can write generated entity models.

            var jack = new Person()
            {
                Id   = 0,
                Name = "Jack",
                Age  = 10,
                Gender = Gender.Male,
                Data = new byte[] { 0, 1, 2, 3, 4 }
            };

            var jill = new Person()
            {
                Id   = 1,
                Name = "Jill",
                Age  = 11,
                Gender = Gender.Female,
                Data = new byte[] { 5, 6, 7, 8, 9 }
            };

            Assert.Equal("Test.Neon.Models.Definitions.Person::0", jack.GetKey());
            Assert.Equal("Test.Neon.Models.Definitions.Person::1", jill.GetKey());

            await bucket.UpsertSafeAsync(jack, persistTo: PersistTo.One);
            await bucket.UpsertSafeAsync(jill, persistTo: PersistTo.One);

            // Verify that we can read them.

            var jackRead = await bucket.GetSafeAsync<Person>(Person.CreateKey(0));
            var jillRead = await bucket.GetSafeAsync<Person>(Person.CreateKey(1));

            Assert.Equal("Test.Neon.Models.Definitions.Person::0", jackRead.GetKey());
            Assert.Equal("Test.Neon.Models.Definitions.Person::1", jillRead.GetKey());
            Assert.True(jack == jackRead);
            Assert.True(jill == jillRead);

            //-----------------------------------------------------------------
            // Persist a [City] entity (which has a different entity type) and then
            // perform a N1QL query to list the Person entities and verify that we
            // get only Jack and Jill back.  This verifies the the [TypeFilter] 
            // attribute is generated and working correctly.

            var city = new City()
            {
                Name       = "Woodinville",
                Population = 12345
            };

            var result = await bucket.UpsertAsync(city);

            await bucket.WaitForIndexerAsync();

            //-----------------------------------------------------------------
            // Query for the people and verify

            var peopleQuery = (from doc in context.Query<Person>() select doc);
            var people      = peopleQuery.ToList();

            Assert.Equal(2, people.Count);
            Assert.Contains(people, p => p.Name == "Jack");
            Assert.Contains(people, p => p.Name == "Jill");

            //-----------------------------------------------------------------
            // Query for the city and verify.

            var cityQuery = from doc in context.Query<City>() select doc;
            var cities    = cityQuery.ToList();

            Assert.Single(cities);
            Assert.Contains(cities, p => p.Name == "Woodinville");

            //-----------------------------------------------------------------
            // Query for documents that don't exist and verify.

            var rawResults   = await bucket.QueryAsync<object>($"select * from `{bucket.Name}` where __T=\"Test.Neon.Models.Definitions.Country\";");
            var countryQuery = from doc in context.Query<Country>() select doc;

            Assert.Empty(rawResults.ToList());
            Assert.Empty(countryQuery.ToList());  // $todo(jefflill): https://github.com/nforgeio/neonKUBE/issues/475

            //-----------------------------------------------------------------
            // Verify that plain old object serialization still works.

            var poo = new PlainOldObject() { Foo = "bar" };

            await bucket.UpsertSafeAsync("poo", poo, persistTo: PersistTo.One);

            poo = await bucket.GetSafeAsync<PlainOldObject>("poo");

            Assert.Equal("bar", poo.Foo);

            //-----------------------------------------------------------------
            // Extra credit #1: Verify that [DeepClone()] works.

            var clone = jack.DeepClone();

            Assert.Equal(jack.Name, clone.Name);
            Assert.Equal(jack.Age, clone.Age);
            Assert.Equal(jack.Gender, clone.Gender);
            Assert.NotSame(jack.Data, clone.Data);

            //-----------------------------------------------------------------
            // Extra credit #2: Verify that [SameTypeAs()] works.

            Assert.True(Person.SameTypeAs(jack));
            Assert.False(Person.SameTypeAs(city));
            Assert.False(Person.SameTypeAs(null));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task CustomNames()
        {
            // Ensure that the database starts out empty.

            Assert.Empty(from doc in context.Query<object>() select doc);

            // Verify that we can write generated entity models.

            var jack = new CustomPerson()
            {
                Id   = 0,
                Name = "Jack",
                Age  = 10,
                Gender = Gender.Male,
                Data = new byte[] { 0, 1, 2, 3, 4 }
            };

            var jill = new CustomPerson()
            {
                Id   = 1,
                Name = "Jill",
                Age  = 11,
                Gender = Gender.Female,
                Data = new byte[] { 5, 6, 7, 8, 9 }
            };

            Assert.Equal("custom-person::0", jack.GetKey());
            Assert.Equal("custom-person::1", jill.GetKey());

            await bucket.UpsertSafeAsync(jack, persistTo: PersistTo.One);
            await bucket.UpsertSafeAsync(jill, persistTo: PersistTo.One);

            // Verify that we can read them.

            var jackRead = await bucket.GetSafeAsync<CustomPerson>(CustomPerson.CreateKey(0));
            var jillRead = await bucket.GetSafeAsync<CustomPerson>(CustomPerson.CreateKey(1));

            Assert.Equal("custom-person::0", jackRead.GetKey());
            Assert.Equal("custom-person::1", jillRead.GetKey());
            Assert.True(jack == jackRead);
            Assert.True(jill == jillRead);

            //-----------------------------------------------------------------
            // Persist a [City] entity (which has a different entity type) and then
            // perform a N1QL query to list the [CustomPerson] entities and verify that
            // we get only Jack and Jill back.  This verifies the the [TypeFilter] 
            // attribute is generated and working correctly.

            var city = new City()
            {
                Name       = "Woodinville",
                Population = 12345
            };

            var result = await bucket.UpsertAsync(city);

            bucket.WaitForIndexer();

            //-----------------------------------------------------------------
            // Query for the people and verify

            var peopleQuery = (from doc in context.Query<CustomPerson>() select doc);
            var people      = peopleQuery.ToList();

            Assert.Equal(2, people.Count);
            Assert.Contains(people, p => p.Name == "Jack");
            Assert.Contains(people, p => p.Name == "Jill");

            //-----------------------------------------------------------------
            // Query for the city and verify.

            var cityQuery = from doc in context.Query<City>() select doc;
            var cities    = cityQuery.ToList();

            Assert.Single(cities);
            Assert.Contains(cities, p => p.Name == "Woodinville");

            //-----------------------------------------------------------------
            // Query for documents that don't exist and verify.

            var rawResults   = await bucket.QueryAsync<object>($"select * from `{bucket.Name}` where __T=\"Test.Neon.Models.Definitions.Country\";");
            var countryQuery = from doc in context.Query<Country>() select doc;

            Assert.Empty(rawResults.ToList());
            Assert.Empty(countryQuery.ToList());

            //-----------------------------------------------------------------
            // Verify that plain old object serialization still works.

            var poo = new PlainOldObject() { Foo = "bar" };

            await bucket.UpsertSafeAsync("poo", poo, persistTo: PersistTo.One);

            poo = await bucket.GetSafeAsync<PlainOldObject>("poo");

            Assert.Equal("bar", poo.Foo);

            //-----------------------------------------------------------------
            // Extra credit #1: Verify that [DeepClone()] works.

            var clone = jack.DeepClone();

            Assert.Equal(jack.Name, clone.Name);
            Assert.Equal(jack.Age, clone.Age);
            Assert.Equal(jack.Gender, clone.Gender);
            Assert.NotSame(jack.Data, clone.Data);

            //-----------------------------------------------------------------
            // Extra credit #2: Verify that [SameTypeAs()] works.

            Assert.True(CustomPerson.SameTypeAs(jack));
            Assert.False(CustomPerson.SameTypeAs(city));
            Assert.False(CustomPerson.SameTypeAs(null));
        }

        public class PeopleList
        {
            public List<Person> List { get; set; } = new List<Person>();
        }

        [Fact(Skip = "TODO: https://github.com/nforgeio/neonKUBE/issues/704")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task RoundTrip_Array()
        {
            // Ensure that the database starts out empty.

            Assert.Empty(from doc in context.Query<object>() select doc);

            // Ensure that object properties that ARE NOT defined in the data model
            // interface are retained when persisted and then read from the database.

            var jack = new Person()
            {
                Id     = 0,
                Name   = "Jack",
                Age    = 10,
                Gender = Gender.Male,
                Data   = new byte[] { 0, 1, 2, 3, 4 }
            };

            jack.__O["Height"] = 182;

            var jill = new Person()
            {
                Id     = 1,
                Name   = "Jill",
                Age    = 11,
                Gender = Gender.Female,
                Data   = new byte[] { 5, 6, 7, 8, 9 }
            };

            jill.__O["Height"] = 185;

            var people = new PeopleList();

            people.List.Add(jack);
            people.List.Add(jill);

            await bucket.UpsertSafeAsync("test-people", people, persistTo: PersistTo.One);

            var peopleRead = await bucket.GetSafeAsync<PeopleList>("test-people");

            Assert.NotNull(peopleRead);
            Assert.NotNull(peopleRead.List);
            Assert.Equal(2, peopleRead.List.Count);

            var jackRead = peopleRead.List[0];

            Assert.Equal(jack.Id, jackRead.Id);
            Assert.Equal(jack.Name, jackRead.Name);
            Assert.Equal(jack.Age, jackRead.Age);
            Assert.Equal(jack.Gender, jackRead.Gender);
            Assert.Equal(jack.Data, jackRead.Data);
            Assert.Equal(182, (int)jackRead.__O["Height"]);
            Assert.Equal(jack, jackRead);

            var jillRead = peopleRead.List[0];

            Assert.Equal(jill.Id, jillRead.Id);
            Assert.Equal(jill.Name, jillRead.Name);
            Assert.Equal(jill.Age, jillRead.Age);
            Assert.Equal(jill.Gender, jillRead.Gender);
            Assert.Equal(jill.Data, jillRead.Data);
            Assert.Equal(185, (int)jillRead.__O["Height"]);
            Assert.Equal(jill, jillRead);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task RoundTrip_Object()
        {
            // Ensure that the database starts out empty.

            Assert.Empty(from doc in context.Query<object>() select doc);

            // Ensure that objects in an array with properties that ARE NOT defined in
            // the data model interface are retained when persisted and then read from
            // the database.

            var jack = new Person()
            {
                Id     = 0,
                Name   = "Jack",
                Age    = 10,
                Gender = Gender.Male,
                Data   = new byte[] { 0, 1, 2, 3, 4 }
            };

            jack.__O["Height"] = 182;

            await bucket.UpsertSafeAsync(jack, persistTo: PersistTo.One);

            var jackRead = await bucket.GetSafeAsync<Person>(Person.CreateKey(0));

            Assert.Equal(jack.Id, jackRead.Id);
            Assert.Equal(jack.Name, jackRead.Name);
            Assert.Equal(jack.Age, jackRead.Age);
            Assert.Equal(jack.Gender, jackRead.Gender);
            Assert.Equal(jack.Data, jackRead.Data);
            Assert.Equal(182, (int)jackRead.__O["Height"]);
            Assert.Equal(jack, jackRead);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task Find()
        {
            // Ensure that the database starts out empty.

            Assert.Empty(from doc in context.Query<object>() select doc);

            // Verify that finding a document that doesn't exist returns NULL.

            Assert.Null(await bucket.FindSafeAsync<Person>(Person.CreateKey("0")));
            Assert.Null(await bucket.FindDocumentSafeAsync<Person>(Person.CreateKey("0")));

            // Verify that finding a document that does exist works.

            var jack = new Person()
            {
                Id   = 0,
                Name = "Jack",
                Age  = 10,
                Gender = Gender.Male,
                Data = new byte[] { 0, 1, 2, 3, 4 }
            };

            await bucket.UpsertSafeAsync(jack, persistTo: PersistTo.One);

            var person = await bucket.FindSafeAsync<Person>(Person.CreateKey("0"));

            Assert.NotNull(person);
            Assert.Equal(jack.Id, person.Id);
            Assert.Equal(jack.Name, person.Name);
            Assert.Equal(jack.Age, person.Age);
            Assert.Equal(jack.Gender, person.Gender);
            Assert.Equal(jack.Data, person.Data);

            var personDoc = await bucket.FindDocumentSafeAsync<Person>(Person.CreateKey("0"));

            Assert.NotNull(personDoc);
            Assert.Equal(jack.Id, personDoc.Content.Id);
            Assert.Equal(jack.Name, personDoc.Content.Name);
            Assert.Equal(jack.Age, personDoc.Content.Age);
            Assert.Equal(jack.Gender, personDoc.Content.Gender);
            Assert.Equal(jack.Data, personDoc.Content.Data);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task Query()
        {
            // Ensure that the database starts out empty.

            Assert.Empty(from doc in context.Query<object>() select doc);

            // Persist some documents for querying.

            var jack = new Person()
            {
                Id   = 0,
                Name = "Jack",
                Age  = 10,
                Gender = Gender.Male,
            };

            await bucket.UpsertSafeAsync(jack, persistTo: PersistTo.One);

            var jill = new Person()
            {
                Id   = 1,
                Name = "Jill",
                Age  = 11,
                Gender = Gender.Female,
            };

            await bucket.UpsertSafeAsync(jill, persistTo: PersistTo.One);

            var john = new Person()
            {
                Id   = 2,
                Name = "John",
                Age  = 12,
                Gender = Gender.Male,
            };

            await bucket.UpsertSafeAsync(john, persistTo: PersistTo.One);

            // Wait for the indexer to catch up.

            await bucket.WaitForIndexerAsync();

            //-----------------------------------------------------------------

            var gotJack = false;
            var gotJill = false;
            var gotJohn = false;

            foreach (var person in (from item in context.Query<Person>() select item))
            {
                gotJack = gotJack || person.Name == jack.Name;
                gotJill = gotJill || person.Name == jill.Name;
                gotJohn = gotJohn || person.Name == john.Name;
            }

            Assert.True(gotJack);
            Assert.True(gotJill);
            Assert.True(gotJohn);

            //-----------------------------------------------------------------

            gotJack = false;
            gotJill = false;
            gotJohn = false;

            foreach (var person in (from item in context.Query<Person>() where item.Name == "Jack" select item))
            {
                gotJack = gotJack || person.Name == jack.Name;
                gotJill = gotJill || person.Name == jill.Name;
                gotJohn = gotJohn || person.Name == john.Name;
            }

            Assert.True(gotJack);
            Assert.False(gotJill);
            Assert.False(gotJohn);

            //-----------------------------------------------------------------

            gotJack = false;
            gotJill = false;
            gotJohn = false;

            foreach (var person in (from item in context.Query<Person>() where item.Age >= 11 select item))
            {
                gotJack = gotJack || person.Name == jack.Name;
                gotJill = gotJill || person.Name == jill.Name;
                gotJohn = gotJohn || person.Name == john.Name;
            }

            Assert.False(gotJack);
            Assert.True(gotJill);
            Assert.True(gotJohn);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task QueryCustom()
        {
            // Ensure that the database starts out empty.

            Assert.Empty(from doc in context.Query<object>() select doc);

            // Persist some documents for querying.

            var jack = new CustomPerson()
            {
                Id   = 0,
                Name = "Jack-Custom",
                Age  = 10,
                Gender = Gender.Male
            };

            await bucket.UpsertSafeAsync(jack, persistTo: PersistTo.One);

            var jill = new CustomPerson()
            {
                Id   = 1,
                Name = "Jill-Custom",
                Age  = 11,
                Gender = Gender.Female
            };

            await bucket.UpsertSafeAsync(jill, persistTo: PersistTo.One);

            var john = new CustomPerson()
            {
                Id   = 2,
                Name = "John-Custom",
                Age  = 12,
                Gender = Gender.Male
            };

            await bucket.UpsertSafeAsync(john, persistTo: PersistTo.One);

            // Wait for the indexer to catch up.

            await bucket.WaitForIndexerAsync();

            //-----------------------------------------------------------------

            var gotJack = false;
            var gotJill = false;
            var gotJohn = false;

            foreach (var person in (from item in context.Query<CustomPerson>() select item))
            {
                gotJack = gotJack || person.Name == jack.Name;
                gotJill = gotJill || person.Name == jill.Name;
                gotJohn = gotJohn || person.Name == john.Name;
            }

            Assert.True(gotJack);
            Assert.True(gotJill);
            Assert.True(gotJohn);

            //-----------------------------------------------------------------

            gotJack = false;
            gotJill = false;
            gotJohn = false;

            foreach (var person in (from item in context.Query<CustomPerson>() where item.Name == "Jack-Custom" select item))
            {
                gotJack = gotJack || person.Name == jack.Name;
                gotJill = gotJill || person.Name == jill.Name;
                gotJohn = gotJohn || person.Name == john.Name;
            }

            Assert.True(gotJack);
            Assert.False(gotJill);
            Assert.False(gotJohn);

            //-----------------------------------------------------------------

            gotJack = false;
            gotJill = false;
            gotJohn = false;

            foreach (var person in (from item in context.Query<CustomPerson>() where item.Age >= 11 select item))
            {
                gotJack = gotJack || person.Name == jack.Name;
                gotJill = gotJill || person.Name == jill.Name;
                gotJohn = gotJohn || person.Name == john.Name;
            }

            Assert.False(gotJack);
            Assert.True(gotJill);
            Assert.True(gotJohn);
        }

        [Fact(Skip = "TODO: https://github.com/nforgeio/neonKUBE/issues/704")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public async Task Dont_Persist_O()
        {
            // Ensure that the database starts out empty.

            Assert.Empty(from doc in context.Query<object>() select doc);

            // Verify that objects written to the database don't include any
            // backing "__O" properties.

            var jack = new Person()
            {
                Id     = 0,
                Name   = "Jack",
                Age    = 10,
                Gender = Gender.Male,
                Data   = new byte[] { 0, 1, 2, 3, 4 }
            };

            var jill = new Person()
            {
                Id     = 1,
                Name   = "Jill",
                Age    = 11,
                Gender = Gender.Female,
                Data   = new byte[] { 5, 6, 7, 8, 9 }
            };

            var family = new Family
            {
                Id     = 0,
                Mother = jill,
                Father = jack,
                Baby   = null
            };

            // Round-trip the family to JObject so that the __O properties will be
            // initialized and then persist them to the database, expecting that
            // these properties will be stripped before the documents are saved.

            var familyJObj = family.ToJObject();
            var familyJson = familyJObj.ToString(Formatting.Indented);

            family = Family.CreateFrom(family.ToJObject());

            Assert.NotEmpty(family.__O);
            Assert.NotEmpty(family.Mother.__O);
            Assert.NotEmpty(family.Father.__O);

            await bucket.UpsertSafeAsync(family);

            // Verify that we can read the family and that it and that none of the members include the "__O" properties.

            familyJson = (await bucket.GetSafeAsync<JObject>(Person.CreateKey(0))).ToString(Formatting.Indented);

            Assert.DoesNotContain("\"__O\"", familyJson);
        }
    }
}