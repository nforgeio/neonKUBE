//-----------------------------------------------------------------------------
// FILE:	    Test_Converters.cs
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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.JsonConverters;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_Converters
    {
        private class TestDateTime
        {
            [JsonConverter(typeof(JsonDateTimeConverter))]
            public DateTime Date { get; set; }
        }

        [Fact]
        public void JsonDateTimeConverter()
        {
            var date = new DateTime(2022, 6, 21, 1, 2, 3, DateTimeKind.Utc);
            var obj  = new TestDateTime()
            {
                Date = date
            };

            var json = JsonSerializer.Serialize(obj);

            Assert.Equal(@"{""Date"":""2022-06-21T01:02:03Z""}", json);

            obj = JsonSerializer.Deserialize<TestDateTime>(json);

            Assert.Equal(date, obj.Date);
        }

        private class TestNullableDateTime
        {
            [JsonConverter(typeof(JsonNullableDateTimeConverter))]
            public DateTime? Date { get; set; }
        }

        [Fact]
        public void JsonNullableDateTimeConverter()
        {
            // Non-NULL tests

            var date = new DateTime(2022, 6, 21, 1, 2, 3, DateTimeKind.Utc);
            var obj  = new TestNullableDateTime()
            {
                Date = date
            };

            var json = JsonSerializer.Serialize(obj);

            Assert.Equal(@"{""Date"":""2022-06-21T01:02:03Z""}", json);

            obj = JsonSerializer.Deserialize<TestNullableDateTime>(json);

            Assert.Equal(date, obj.Date);

            // NULL tests

            obj = new TestNullableDateTime()
            {
                Date = null
            };

            json = JsonSerializer.Serialize(obj);

            Assert.Equal(@"{""Date"":null}", json);

            obj = JsonSerializer.Deserialize<TestNullableDateTime>(json);

            Assert.Null(obj.Date);
        }
    }
}
