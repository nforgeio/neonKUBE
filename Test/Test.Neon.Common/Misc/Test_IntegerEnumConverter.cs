//-----------------------------------------------------------------------------
// FILE:	    Test_IntegerEnumConverter.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.Xunit;

using Newtonsoft.Json;

using Xunit;

namespace TestCommon
{
    public class Test_IntegerEnumConverter
    {
        //---------------------------------------------------------------------
        // Local types

        public enum MyEnum
        {
            Zero = 0,
            One = 1,
            Two = 2
        }

        public class MyData
        {
            [JsonConverter(typeof(IntegerEnumConverter<MyEnum>))]
            public MyEnum Value { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private JsonSerializer serializer = new JsonSerializer();

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ReadInt()
        {
            var json0 = "{ \"Value\": 0 }";
            var json1 = "{ \"Value\": 1 }";
            var json2 = "{ \"Value\": 2 }";

            var data = serializer.Deserialize<MyData>(CreateReader(json0));

            Assert.Equal(MyEnum.Zero, data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json1));

            Assert.Equal(MyEnum.One, data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json2));

            Assert.Equal(MyEnum.Two, data.Value);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ReadString()
        {
            var json0 = "{ \"Value\": \"Zero\" }";
            var json1 = "{ \"Value\": \"One\" }";
            var json2 = "{ \"Value\": \"Two\" }";

            var data = serializer.Deserialize<MyData>(CreateReader(json0));

            Assert.Equal(MyEnum.Zero, data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json1));

            Assert.Equal(MyEnum.One, data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json2));

            Assert.Equal(MyEnum.Two, data.Value);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ReadString_Insensitive()
        {
            // Verify that parsing is case insensitive.

            var json0 = "{ \"Value\": \"zero\" }";
            var json1 = "{ \"Value\": \"ONE\" }";
            var json2 = "{ \"Value\": \"tWO\" }";

            var data = serializer.Deserialize<MyData>(CreateReader(json0));

            Assert.Equal(MyEnum.Zero, data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json1));

            Assert.Equal(MyEnum.One, data.Value);

            data = serializer.Deserialize<MyData>(CreateReader(json2));

            Assert.Equal(MyEnum.Two, data.Value);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Write()
        {
            var value  = new MyData() { Value = MyEnum.Two };
            var sb     = new StringBuilder();
            var writer = new StringWriter(sb);
            
            serializer.Serialize(writer, value);

            var json = sb.ToString();

            Assert.Equal("{\"Value\":\"Two\"}", json);
        }

        private JsonReader CreateReader(string json)
        {
            return new JsonTextReader(new StringReader(json));
        }
    }
}
