//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.Json.cs
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

using Newtonsoft.Json;

using Neon.Common;
using Neon.Xunit;

using Xunit;
using System.Text;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        public class JsonTestPerson
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonSerializeStrict()
        {
            var before = 
                new JsonTestPerson()
                {
                    Name = "Jeff",
                    Age  = 56
                };

            var json = NeonHelper.JsonSerialize(before);

            Assert.StartsWith("{", json);

            var after = NeonHelper.JsonDeserialize<JsonTestPerson>(json);

            Assert.Equal("Jeff", after.Name);
            Assert.Equal(56, after.Age);

            const string unmatchedJson =
@"
{
    ""Name"": ""jeff"",
    ""Age"": 66,
    ""Unmatched"": ""Hello""
}
";
            // Verify that we don't see exceptions for a source property
            // that's not defined in the type by default (when [strict=false])

            NeonHelper.JsonDeserialize<JsonTestPerson>(unmatchedJson);

            // Verify that we see exceptions for a source property
            // that's not defined in the type when [strict=true]

            Assert.Throws<JsonSerializationException>(() => NeonHelper.JsonDeserialize<JsonTestPerson>(unmatchedJson, strict: true));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonSerializeBytesStrict()
        {
            var before =
                new JsonTestPerson()
                {
                    Name = "Jeff",
                    Age  = 56
                };

            var jsonBytes = NeonHelper.JsonSerializeToBytes(before);

            Assert.StartsWith("{", Encoding.UTF8.GetString(jsonBytes));

            var after = NeonHelper.JsonDeserialize<JsonTestPerson>(jsonBytes);

            Assert.Equal("Jeff", after.Name);
            Assert.Equal(56, after.Age);

            const string unmatchedJson =
@"
{
    ""Name"": ""jeff"",
    ""Age"": 66,
    ""Unmatched"": ""Hello""
}
";
            var unmatchedJsonBytes = Encoding.UTF8.GetBytes(unmatchedJson);

            // Verify that we don't see exceptions for a source property
            // that's not defined in the type by default (when [strict=false])

            NeonHelper.JsonDeserialize<JsonTestPerson>(unmatchedJsonBytes);

            // Verify that we see exceptions for a source property
            // that's not defined in the type when [strict=true]

            Assert.Throws<JsonSerializationException>(() => NeonHelper.JsonDeserialize<JsonTestPerson>(unmatchedJsonBytes, strict: true));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonSerializeRelaxed()
        {
            var before =
                new JsonTestPerson()
                {
                    Name = "Jeff",
                    Age  = 56
                };

            var json = NeonHelper.JsonSerialize(before);

            Assert.StartsWith("{", json);

            var after = NeonHelper.JsonDeserialize<JsonTestPerson>(json);

            Assert.Equal("Jeff", after.Name);
            Assert.Equal(56, after.Age);

            // Verify that we don't see exceptions for a source property
            // that's not defined in the type.

            const string unmatchedJson =
@"
{
    ""Name"": ""jeff"",
    ""Age"": 66,
    ""Unmatched"": ""Hello""
}
";
            NeonHelper.JsonDeserialize<JsonTestPerson>(unmatchedJson, strict: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonSerializeBytesRelaxed()
        {
            var before =
                new JsonTestPerson()
                {
                    Name = "Jeff",
                    Age = 56
                };

            var jsonBytes = NeonHelper.JsonSerializeToBytes(before);

            Assert.StartsWith("{", Encoding.UTF8.GetString(jsonBytes));

            var after = NeonHelper.JsonDeserialize<JsonTestPerson>(jsonBytes);

            Assert.Equal("Jeff", after.Name);
            Assert.Equal(56, after.Age);

            // Verify that we don't see exceptions for a source property
            // that's not defined in the type.

            const string unmatchedJson =
@"
{
    ""Name"": ""jeff"",
    ""Age"": 66,
    ""Unmatched"": ""Hello""
}
";
            var unmatchedJsonBytes = Encoding.UTF8.GetBytes(unmatchedJson);

            NeonHelper.JsonDeserialize<JsonTestPerson>(unmatchedJsonBytes, strict: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonClone()
        {
            var value = 
                new JsonTestPerson()
                {
                    Name = "Jeff",
                    Age  = 56
                };

            var clone = NeonHelper.JsonClone<JsonTestPerson>(value);

            Assert.NotSame(value, clone);
            Assert.Equal(value.Name, clone.Name);
            Assert.Equal(value.Age, clone.Age);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonNotYaml()
        {
            // Verify that we can identify and parse JSON (over YAML).

            var before =
                new JsonTestPerson()
                {
                    Name = "Jeff",
                    Age = 56
                };

            var json = NeonHelper.JsonSerialize(before);

            Assert.StartsWith("{", json);

            var after = NeonHelper.JsonOrYamlDeserialize<JsonTestPerson>(json);

            Assert.Equal("Jeff", after.Name);
            Assert.Equal(56, after.Age);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ExtendedConverters()
        {
            // Verify that the extended type converters work.

            string value;

            //------------------------------------------------------------------

            var semanticVersion = SemanticVersion.Create(1, 2, 3, "build-0", "alpha");

            value = NeonHelper.JsonSerialize(semanticVersion);
            Assert.Equal(semanticVersion, NeonHelper.JsonDeserialize<SemanticVersion>(value));

            //------------------------------------------------------------------

            var timespan = TimeSpan.FromDays(2.1234567);

            value = NeonHelper.JsonSerialize(timespan);
            Assert.Equal(timespan, NeonHelper.JsonDeserialize<TimeSpan>(value));

            //------------------------------------------------------------------

            var version = new Version(1, 2, 3);

            value = NeonHelper.JsonSerialize(version);
            Assert.Equal(version, NeonHelper.JsonDeserialize<Version>(value));
        }
    }
}
