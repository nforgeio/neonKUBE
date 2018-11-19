//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.Yaml.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using YamlDotNet.Core;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_Helper
    {
        public class YamlPerson
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void YamlSerialize()
        {
            var before =
                new YamlPerson()
                {
                    Name = "Jeff",
                    Age  = 56
                };

            // Verify that the property names were converted to lowercase.

            var yaml = NeonHelper.YamlSerialize(before);

            Assert.Contains("name: Jeff", yaml);
            Assert.Contains("age: 56", yaml);

            // Verify that we can deserialize.

            var after = NeonHelper.YamlDeserialize<YamlPerson>(yaml);

            Assert.Equal("Jeff", after.Name);
            Assert.Equal(56, after.Age);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void YamlDeserializeUnmatched()
        {
            const string normal =
@"name: Jeff
age: 56
";
            const string unmatched =
@"name: Jeff
age: 56
unmatched: Hello
";
            // Verify that we can deserialize YAML without unmatched properties.

            var person = NeonHelper.YamlDeserialize<YamlPerson>(normal);

            Assert.Equal("Jeff", person.Name);
            Assert.Equal(56, person.Age);

            // Verify that we can ignore unmatched properties.

            person = NeonHelper.YamlDeserialize<YamlPerson>(unmatched, strict: false);

            Assert.Equal("Jeff", person.Name);
            Assert.Equal(56, person.Age);

            // Verify that we see an exception when we're not ignoring unmatched
            // properties and the input has an unmatched property.

            Assert.Throws<YamlException>(() => NeonHelper.YamlDeserialize<YamlPerson>(unmatched, strict: true));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void YamlException()
        {
            // Verify that we beautify exception messages. 

            const string unmatched =
@"name: Jeff
age: 56
unmatched: Hello
";
            try
            {
                var person = NeonHelper.YamlDeserialize<YamlPerson>(unmatched, strict: true);
            }
            catch (YamlException e)
            {
                Assert.StartsWith("(line: ", e.Message);
            }

            const string badSyntax =
@"name Jeff
age: 56
";
            try
            {
                var person = NeonHelper.YamlDeserialize<YamlPerson>(badSyntax);
            }
            catch (YamlException e)
            {
                Assert.StartsWith("(line: ", e.Message);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void YamlNotJson()
        {
            // Verify that we can identify and parse YAML (over JSON).

            var before =
                new YamlPerson()
                {
                    Name = "Jeff",
                    Age = 56
                };

            // Verify that the property names were converted to lowercase.

            var yaml = NeonHelper.YamlSerialize(before);

            Assert.Contains("name: Jeff", yaml);
            Assert.Contains("age: 56", yaml);

            // Verify that we can deserialize.

            var after = NeonHelper.JsonOrYamlDeserialize<YamlPerson>(yaml);

            Assert.Equal("Jeff", after.Name);
            Assert.Equal(56, after.Age);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void YamlArray()
        {
            // Verify that we can YAML arrays.

            var before = new List<YamlPerson>();

            before.Add(
                new YamlPerson()
                {
                    Name = "Jeff",
                    Age = 56
                });

            before.Add(
                new YamlPerson()
                {
                    Name = "Darrian",
                    Age = 25
                });

            var yaml = NeonHelper.YamlSerialize(before);

            // Verify that we can deserialize.

            var after = NeonHelper.JsonOrYamlDeserialize<List<YamlPerson>>(yaml);

            Assert.Equal(2, after.Count);
            Assert.Equal("Jeff", after[0].Name);
            Assert.Equal("Darrian", after[1].Name);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonToYaml()
        {
            // Verify that we can convert JSON to YAML.

            var input = new YamlPerson()
            {
                Name = "Jeff",
                Age = 56
            };

            var jsonText = NeonHelper.JsonSerialize(input);
            var yamlText = NeonHelper.JsonToYaml(NeonHelper.JsonDeserialize<dynamic>(jsonText));
            var output   = NeonHelper.YamlDeserialize<YamlPerson>(yamlText);

            Assert.Equal(input.Name, output.Name);
            Assert.Equal(input.Age, output.Age);
        }
    }
}
