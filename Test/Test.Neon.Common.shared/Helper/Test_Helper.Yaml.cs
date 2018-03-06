//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.Yaml.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;

using Neon.Common;

using Xunit;
using YamlDotNet.Core;

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

            person = NeonHelper.YamlDeserialize<YamlPerson>(unmatched, ignoreUnmatched: true);

            Assert.Equal("Jeff", person.Name);
            Assert.Equal(56, person.Age);

            // Verify that we see an exception when we're not ignoring unmatched
            // properties and the input has an unmatched property.

            Assert.Throws<YamlException>(() => NeonHelper.YamlDeserialize<YamlPerson>(unmatched, ignoreUnmatched: false));
        }

        [Fact]
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
                var person = NeonHelper.YamlDeserialize<YamlPerson>(unmatched, ignoreUnmatched: false);
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
    }
}
