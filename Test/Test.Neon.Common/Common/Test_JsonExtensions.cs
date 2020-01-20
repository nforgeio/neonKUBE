//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_JsonExtensions
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void TryGetValue()
        {
            const string jsonText =
@"{
    ""int"": 10,
    ""long"": 6000000000,
    ""string"": ""Hello World!"",
    ""null"": null,
    ""bool-true"": true,
    ""bool-false"": false,
    ""float"": 123.456
}
";
            var jObject = JObject.Parse(jsonText);

            Assert.True(jObject.TryGetValue<int>("int", out var iValue));
            Assert.Equal(10, iValue);
            Assert.Throws<FormatException>(() => jObject.TryGetValue<int>("string", out iValue));

            Assert.True(jObject.TryGetValue<long>("long", out var lValue));
            Assert.Equal(6000000000L, lValue);
            Assert.Throws<FormatException>(() => jObject.TryGetValue<int>("string", out iValue));

            Assert.True(jObject.TryGetValue<string>("string", out var sValue));
            Assert.Equal("Hello World!", sValue);
            Assert.True(jObject.TryGetValue<string>("bool-true", out sValue));
            Assert.Equal("True", sValue);
            Assert.True(jObject.TryGetValue<string>("null", out sValue));
            Assert.Null(sValue);

            Assert.True(jObject.TryGetValue<bool>("bool-true", out var bValue));
            Assert.True(bValue);
            Assert.True(jObject.TryGetValue<bool>("bool-false", out bValue));
            Assert.False(bValue);

            var delta = 0.0001;     // Floating point operations aren't precise.

            Assert.True(jObject.TryGetValue<float>("float", out var fValue));
            Assert.InRange(fValue, 123.456 - delta, 123.456 + delta);

            Assert.True(jObject.TryGetValue<double>("float", out var dValue));
            Assert.InRange(fValue, 123.456 - delta, 123.456 + delta);

            // Verify that we detect when a property doesn't exist.

            Assert.False(jObject.TryGetValue<int>("does-not-exist", out iValue));
        }
    }
}
