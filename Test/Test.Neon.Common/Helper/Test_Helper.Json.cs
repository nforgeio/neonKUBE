//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.Json.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_Helper
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
    }
}
