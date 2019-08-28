//-----------------------------------------------------------------------------
// FILE:        Test_JsonDataConverter.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

using Test.Neon.Models;
using Newtonsoft.Json.Linq;

namespace TestCadence
{
    public class Test_JsonDataConverter
    {
        public class TestData
        {
            public string Hello { get; set; }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void DataWithGenericType()
        {
            // Verify that we can serialize and deserialize various (non-roundtrip) data items
            // using the generic FromData() method.

            var     converter = new JsonDataConverter();
            byte[]  contents;

            // Serialize NULL.

            contents = converter.ToData(null);
            Assert.Null(converter.FromData<object>(contents));

            // Serialize a string.

            contents = converter.ToData("foo");
            Assert.Equal("foo", converter.FromData<string>(contents));

            // Serialize a byte array.

            var items = new int[] { 0, 1, 2, 3, 4 };

            contents = converter.ToData(items);
            Assert.Equal(items, converter.FromData<int[]>(contents));

            // Serialize an array of non-roundtrip objects.

            var items2 = new TestData[] { new TestData() { Hello = "World!" }, new TestData() { Hello = "Goodbye!" }, null };

            contents = converter.ToData(items2);
            items2   = converter.FromData<TestData[]>(contents);
            Assert.Equal(3, items2.Length);
            Assert.Equal("World!", items2[0].Hello);
            Assert.Equal("Goodbye!", items2[1].Hello);
            Assert.Null(items2[2]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void DataWithType()
        {
            // Verify that we can serialize and deserialize various (non-roundtrip) data items
            // using the non-generic FromData() method.

            var     converter = new JsonDataConverter();
            byte[]  contents;

            // Serialize NULL.

            contents = converter.ToData(null);
            Assert.Null(converter.FromData(typeof(object), contents));

            // Serialize a string.

            contents = converter.ToData("foo");
            Assert.Equal("foo", (string)converter.FromData(typeof(string), contents));

            // Serialize a byte array.

            var items = new int[] { 0, 1, 2, 3, 4 };

            contents = converter.ToData(items);
            Assert.Equal(items, (int[])converter.FromData(typeof(int[]), contents));

            // Serialize an array of non-roundtrip objects.

            var items2 = new TestData[] { new TestData() { Hello = "World!" }, new TestData() { Hello = "Goodbye!" }, null };

            contents = converter.ToData(items2);
            items2   = (TestData[])converter.FromData(typeof(TestData[]), contents);
            Assert.Equal(3, items2.Length);
            Assert.Equal("World!", items2[0].Hello);
            Assert.Equal("Goodbye!", items2[1].Hello);
            Assert.Null(items2[2]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void RoundTripDataWithGeneric()
        {
            // Verify that we can serialize and deserialize various roundtrip data items
            // using the generic converter.

            var     converter = new JsonDataConverter();
            byte[]  contents;

            // Serialize NULL.

            contents = converter.ToData(null);
            Assert.Null(converter.FromData<Person>(contents));

            // Serialize a roundtrip item without extra data.

            var bob = new Person()
            {
                Name   = "Bob",
                Age    = 27,
                Data   = new byte[] { 0, 1, 2, 3, 4 },
                Gender = Gender.Male
            };

            contents = converter.ToData(bob);
            Assert.Equal(bob, converter.FromData<Person>(contents));

            // Serialize a roundtrip item WITH extra data.

            bob.__JObject.Add("foo", "bar");

            contents = converter.ToData(bob);

            var deserializedBob = converter.FromData<Person>(contents);

            Assert.Equal(bob, deserializedBob);
            Assert.Equal("bar", (string)deserializedBob.__JObject["foo"]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void RoundTripDataWithType()
        {
            // Verify that we can serialize and deserialize various roundtrip data items
            // using the non-generic converter.

            var     converter = new JsonDataConverter();
            byte[]  contents;

            // Serialize NULL.

            contents = converter.ToData(null);
            Assert.Null(converter.FromData<Person>(contents));

            // Serialize a roundtrip item without extra data.

            var bob = new Person()
            {
                Name   = "Bob",
                Age    = 27,
                Data   = new byte[] { 0, 1, 2, 3, 4 },
                Gender = Gender.Male
            };

            contents = converter.ToData(bob);
            Assert.Equal(bob, (Person)converter.FromData(typeof(Person), contents));

            // Serialize a roundtrip item WITH extra data.

            bob.__JObject.Add("foo", "bar");

            contents = converter.ToData(bob);

            var deserializedBob = (Person)converter.FromData(typeof(Person), contents);

            Assert.Equal(bob, deserializedBob);
            Assert.Equal("bar", (string)deserializedBob.__JObject["foo"]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void DataArray()
        {
            // Verify that we can deserialize arrays of data items.

            var         converter = new JsonDataConverter();
            JArray      jArray;
            byte[]      contents;
            object[]    items;

            // Empty array.

            jArray   = new JArray();
            contents = Encoding.UTF8.GetBytes(jArray.ToString());
            items    = converter.FromDataArray(contents, new Type[0]);

            Assert.Empty(items);

            // Single item.

            jArray = new JArray();
            jArray.Add("foo");

            contents = Encoding.UTF8.GetBytes(jArray.ToString());
            items    = converter.FromDataArray(contents, new Type[] { typeof(string) });

            Assert.Equal(new string[] { "foo" }, items);

            // Multiple object items

            jArray = new JArray();
            jArray.Add("foo");
            jArray.Add(1234);
            jArray.Add(JToken.FromObject(new TestData() { Hello = "World!" }));
            jArray.Add(null);

            contents = Encoding.UTF8.GetBytes(jArray.ToString());
            items    = converter.FromDataArray(contents, new Type[] { typeof(string), typeof(int), typeof(TestData), typeof(TestData) });

            Assert.Equal(4, items.Length);
            Assert.Equal("foo", items[0]);
            Assert.Equal(1234, items[1]);
            Assert.Equal("World!", ((TestData)items[2]).Hello);
            Assert.Null(items[3]);

            // Roundtrip objects

            var bob = new Person()
            {
                Name   = "Bob",
                Age    = 27,
                Data   = new byte[] { 0, 1, 2, 3, 4 },
                Gender = Gender.Male
            };

            bob.__JObject.Add("extra", "data");

            jArray = new JArray();
            jArray.Add("foo");
            jArray.Add(1234);
            jArray.Add(bob.ToJObject());
            jArray.Add(null);

            contents = Encoding.UTF8.GetBytes(jArray.ToString());
            items    = converter.FromDataArray(contents, new Type[] { typeof(string), typeof(int), typeof(Person), typeof(Person) });

            Assert.Equal(4, items.Length);
            Assert.Equal("foo", items[0]);
            Assert.Equal(1234, items[1]);
            Assert.Equal(bob, (Person)items[2]);
            Assert.Equal("data", ((Person)items[2]).__JObject["extra"].ToString());
            Assert.Null(items[3]);

            // Arrays of other types.

            var guid = Guid.NewGuid();

            jArray = new JArray();
            jArray.Add(10);
            jArray.Add(123.4);
            jArray.Add("Hello World!");
            jArray.Add(null);
            jArray.Add(Gender.Female);
            jArray.Add(true);
            jArray.Add(new DateTime(2019, 7, 17, 12, 0, 0));
            jArray.Add(TimeSpan.FromSeconds(1.5));
            jArray.Add(guid);
            
            contents = Encoding.UTF8.GetBytes(jArray.ToString());
            items    = converter.FromDataArray(contents, new Type[] { typeof(int), typeof(double), typeof(string), typeof(string), typeof(Gender), typeof(bool), typeof(DateTime), typeof(TimeSpan), typeof(Guid) });

            Assert.Equal(9, items.Length);
            Assert.Equal(10, (int)items[0]);
            Assert.Equal(123.4, (double)items[1]);
            Assert.Equal("Hello World!", (string)items[2]);
            Assert.Null((string)items[3]);
            Assert.Equal(Gender.Female, (Gender)items[4]);
            Assert.True((bool)items[5]);
            Assert.Equal(new DateTime(2019, 7, 17, 12, 0, 0), (DateTime)items[6]);
            Assert.Equal(TimeSpan.FromSeconds(1.5), (TimeSpan)items[7]);
            Assert.Equal(guid, (Guid)items[8]);
        }
    }
}
