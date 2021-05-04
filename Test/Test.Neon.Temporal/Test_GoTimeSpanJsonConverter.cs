//-----------------------------------------------------------------------------
// FILE:        Test_GoTimeSpanJsonConverter.cs
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
using System.IO;
using System.Text;

using Neon.Data;
using Neon.Temporal;
using Neon.Xunit;

using Newtonsoft.Json;
using Xunit;

namespace TestTemporal
{
    [Trait(TestTrait.Incomplete, "1")]
    [Trait(TestTrait.Area, TestArea.NeonTemporal)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_GoTimeSpanJsonConverter
    {
        public class MyData
        {
            [JsonConverter(typeof(GoTimeSpanJsonConverter))]
            public TimeSpan Value { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private JsonSerializer serializer = new JsonSerializer();

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        public void ReadTimeSpan()
        {
            var json0 = "{ \"Value\": 5 }";
            var json1 = "{ \"Value\": 5000000000 }";
            var json2 = "{ \"Value\": 50000000000000000 }";
            var json3 = "{ \"Value\": 9223372036854775807 }";
            var json4 = "{ \"Value\": 0 }";
            var json5 = "{ \"Value\": -50000000000000000 }";
            var json6 = "{ \"Value\": -9223372036854775808 }";
            var json7 = "{ \"Value\": 92233720368547758070 }";
            var json8 = "{ \"Value\": -92233720368547758070 }";

            var data = serializer.Deserialize<MyData>(CreateReader(json0));

            Assert.Equal(TimeSpan.Zero, data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json1));

            Assert.Equal(TimeSpan.FromSeconds(5), data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json2));

            Assert.Equal(TimeSpan.FromTicks(500000000000000L), data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json3));

            Assert.Equal(TimeSpan.FromTicks(Int64.MaxValue/100L), data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json4));

            Assert.Equal(TimeSpan.Zero, data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json5));

            Assert.Equal(TimeSpan.FromTicks(-500000000000000L), data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json6));

            Assert.Equal(TimeSpan.FromTicks(Int64.MinValue / 100L), data.Value);
            
            Assert.ThrowsAny<Exception>(() => serializer.Deserialize<MyData>(CreateReader(json7)));
            Assert.ThrowsAny<Exception>(() => serializer.Deserialize<MyData>(CreateReader(json8)));
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        public void WriteTimeSpan()
        {
            var value  = new MyData() { Value = TimeSpan.FromSeconds(5) };
            var sb     = new StringBuilder();
            var writer = new StringWriter(sb);

            serializer.Serialize(writer, value);

            var json = sb.ToString();

            Assert.Equal("{\"Value\":5000000000}", json);

            value  = new MyData() { Value = TimeSpan.FromTicks(Int64.MaxValue/100L) };
            sb     = new StringBuilder();
            writer = new StringWriter(sb);

            serializer.Serialize(writer, value);

            json = sb.ToString();

            Assert.Equal("{\"Value\":9223372036854775800}", json);

            value  = new MyData() { Value = TimeSpan.FromTicks(Int64.MinValue / 100L) };
            sb     = new StringBuilder();
            writer = new StringWriter(sb);

            serializer.Serialize(writer, value);

            json = sb.ToString();

            Assert.Equal("{\"Value\":-9223372036854775800}", json);

            value  = new MyData() { Value = TimeSpan.FromSeconds(500055000) };
            sb     = new StringBuilder();
            writer = new StringWriter(sb);

            serializer.Serialize(writer, value);

            json = sb.ToString();

            Assert.Equal("{\"Value\":500055000000000000}", json);

            value  = new MyData() { Value = TimeSpan.FromSeconds(0) };
            sb     = new StringBuilder();
            writer = new StringWriter(sb);

            serializer.Serialize(writer, value);

            json = sb.ToString();

            Assert.Equal("{\"Value\":0}", json);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        public void TimeSpanToSimpleString()
        {
            var json0 = "{ \"Value\": 5 }";
            var json1 = "{ \"Value\": 5000000000 }";
            var json2 = "{ \"Value\": 50000000000000000 }";
            var json3 = "{ \"Value\": 9223372036854775807 }";
            var json4 = "{ \"Value\": 0 }";
            var json5 = "{ \"Value\": -50000000000000000 }";
            var json6 = "{ \"Value\": -9223372036854775808 }";

            var gtjc = new GoTimeSpanJsonConverter();

            var data = serializer.Deserialize<MyData>(CreateReader(json0));

            Assert.Equal(TimeSpan.Zero.ToString("c"), gtjc.ToSimpleString(data.Value));

            data = serializer.Deserialize<MyData>(CreateReader(json1));

            Assert.Equal(TimeSpan.FromSeconds(5).ToString("c"), gtjc.ToSimpleString(data.Value));

            data = serializer.Deserialize<MyData>(CreateReader(json2));

            Assert.Equal(TimeSpan.FromSeconds(50000000).ToString("c"), gtjc.ToSimpleString(data.Value));

            data = serializer.Deserialize<MyData>(CreateReader(json3));

            Assert.Equal(TimeSpan.FromTicks(Int64.MaxValue / 100L).ToString("c"), gtjc.ToSimpleString(data.Value));

            data = serializer.Deserialize<MyData>(CreateReader(json4));

            Assert.Equal(TimeSpan.Zero.ToString("c"), gtjc.ToSimpleString(data.Value));

            data = serializer.Deserialize<MyData>(CreateReader(json5));

            Assert.Equal(TimeSpan.FromTicks(-500000000000000L).ToString("c"), gtjc.ToSimpleString(data.Value));

            data = serializer.Deserialize<MyData>(CreateReader(json6));

            Assert.Equal(TimeSpan.FromTicks(Int64.MinValue / 100L).ToString("c"), gtjc.ToSimpleString(data.Value));
        }

        private JsonReader CreateReader(string json)
        {
            return new JsonTextReader(new StringReader(json));
        }
    }
}
