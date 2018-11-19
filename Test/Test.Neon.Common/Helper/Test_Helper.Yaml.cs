//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.Yaml.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;

using Neon.Common;
using Neon.Xunit;

using YamlDotNet.Core;
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
        public void JsonToYaml_Basic()
        {
            // Verify that we can convert JSON to YAML.

            var input = new YamlPerson()
            {
                Name = "Jeff",
                Age  = 56
            };

            var jsonText = NeonHelper.JsonSerialize(input);
            var yamlText = NeonHelper.JsonToYaml(jsonText);
            var output   = NeonHelper.YamlDeserialize<YamlPerson>(yamlText);

            Assert.Equal(input.Name, output.Name);
            Assert.Equal(input.Age, output.Age);
            NeonHelper.YamlDeserialize<dynamic>(yamlText);

            // Ensure that JSON property strings that are integer values  
            // retain their "stringness".

            input = new YamlPerson()
            {
                Name = "1001",
                Age  = 56
            };

            jsonText = NeonHelper.JsonSerialize(input);
            yamlText = NeonHelper.JsonToYaml(jsonText);

            Assert.Contains("'1001'", yamlText);
            NeonHelper.YamlDeserialize<dynamic>(yamlText);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonToYaml_Values()
        {
            var jsonText = @"
{
    ""int"": 10,
    ""float"": 123.4,
    ""string0"": ""hello world"",
    ""string1"": ""hello \""world\"""",
    ""string2"": ""hello world!"",
    ""string3"": ""test=value"",
    ""string4"": ""line1\nline2\n"",
    ""bool0"": true,
    ""bool1"": false,
    ""is-null"" : null,
    ""string-int"": ""3"",
    ""string-float"": ""123.4"",
    ""string-bool-true"": ""true"",
    ""string-bool-false"": ""false"",
    ""string-bool-yes"": ""yes"",
    ""string-bool-no"": ""no"",
    ""string-bool-on"": ""on"",
    ""string-bool-off"": ""off"",
    ""string-bool-yes"": ""yes"",
    ""string-bool-no"": ""no"",
}
";
            var yamlText = NeonHelper.JsonToYaml(jsonText);
            var yamlExpected =
@"int: 10
float: 123.4
string0: hello world
string1: ""hello \""world\""""
string2: ""hello world!""
string3: ""test=value""
string4: ""line1\nline2\n""
bool0: true
bool1: false
is-null: null
string-int: '3'
string-float: '123.4'
string-bool-true: 'true'
string-bool-false: 'false'
string-bool-on: 'on'
string-bool-off: 'off'
string-bool-yes: 'yes'
string-bool-no: 'no'
";
            Assert.Equal(yamlExpected, yamlText);
            NeonHelper.YamlDeserialize<dynamic>(yamlText);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonToYaml_Object()
        {
            var jsonText = @"
{
  ""name"": ""level0"",
  ""nested"": {
    ""property0"": ""hello"",
    ""property1"": ""world"",
    ""property2"": {
      ""hello"": ""world""
    }
  }
}
";
            var yamlText = NeonHelper.JsonToYaml(jsonText);
            var yamlExpected =
@"name: level0
nested:
  property0: hello
  property1: world
  property2:
    hello: world
";
            Assert.Equal(yamlExpected, yamlText);
            NeonHelper.YamlDeserialize<dynamic>(yamlText);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonToYaml_SimpleArray()
        {
            var jsonText = @"
[
    ""zero"",
    ""one"",
    ""two"",
    ""three""
]
";
            var yamlText = NeonHelper.JsonToYaml(jsonText);
            var yamlExpected =
@"- zero
- one
- two
- three
";
            Assert.Equal(yamlExpected, yamlText);
            NeonHelper.YamlDeserialize<dynamic>(yamlText);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void JsonToYaml_ObjectsAndArrays()
        {
            var jsonText = @"
[
    ""zero"",
    {
      ""name"": ""jeff"",
      ""age"": 56,
      ""pets"": [
        ""lilly"",
        ""butthead"",
        ""poophead"",
        {
           ""name"": ""norman"",
           ""type"": ""pony""
        }
      ]
    }
]
";
            var yamlText = NeonHelper.JsonToYaml(jsonText);
            var yamlExpected =
@"- zero
- name: jeff
  age: 56
  pets:
    - lilly
    - butthead
    - poophead
    - name: norman
      type: pony
";
            Assert.Equal(yamlExpected, yamlText);
            NeonHelper.YamlDeserialize<dynamic>(yamlText);
        }
    }
}
