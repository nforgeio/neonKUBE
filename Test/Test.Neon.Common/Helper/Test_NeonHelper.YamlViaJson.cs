//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.YamlViaJson.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Dynamic;
using System.Linq;
using System.Runtime.Serialization;

using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using YamlDotNet.Core;
using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Gender
        {
            [EnumMember(Value = "female")]
            Female,

            [EnumMember(Value = "male")]
            Male,
        }

        [JsonConverter(typeof(IPersonConverter))]
        public interface IPerson
        {
            string Name { get; set; }
            int Age { get; set; }
            Gender Gender { get; set; }
        }

        public class Man : IPerson
        {
            public Man()
            {
                Gender = Gender.Male;
            }

            public string Name { get; set; }
            public int Age { get; set; }
            public Gender Gender { get; set; }
            public string Height { get; set; }
        }

        public class Woman : IPerson
        {
            public Woman()
            {
                Gender = Gender.Female;
            }

            public string Name { get; set; }
            public int Age { get; set; }
            public Gender Gender { get; set; }
            public string HairColor { get; set; }
        }

        public class IPersonConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(IPerson);
            }

            public override object ReadJson(JsonReader reader,
                   Type objectType, object existingValue,
                   JsonSerializer serializer)
            {
                var jsonObject = JObject.Load(reader);
                IPerson person;

                var value = jsonObject.Value<string>("gender");
                var type = NeonHelper.ParseEnum<Gender>(value);
                switch (type)
                {
                    case Gender.Male:
                        person = new Man();
                        break;
                    case Gender.Female:
                        person = new Woman();
                        break;
                    default:
                        throw new Exception();
                }

                serializer.Populate(jsonObject.CreateReader(), person);
                return person;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
            }
        }

        [Fact]
        public void DeserializeYamlViaJson()
        {
            var people = new List<IPerson>
            {
                new Man() { Name = "Marcus", Age = 10, Height = @"6'7""" },
                new Woman() { Name = "Bridgette", Age = 20, HairColor = "brown" }
            };
            
            var s = NeonHelper.YamlSerialize(people);
            NeonHelper.YamlDeserializeViaJson<List<IPerson>>(s);
            Assert.Throws<YamlException>(() => NeonHelper.YamlDeserialize<List<IPerson>>(s));
            var exception = Record.Exception(() => NeonHelper.YamlDeserializeViaJson<List<IPerson>>(s));
            Assert.Null(exception);

            people = NeonHelper.YamlDeserializeViaJson<List<IPerson>>(s);

            Assert.Single(people.Where(p => p.Gender == Gender.Male));
            Assert.Single(people.Where(p => p.Gender == Gender.Female));

            var man = (Man)people.Where(p => p.Gender == Gender.Male).FirstOrDefault();
            var woman = (Woman)people.Where(p => p.Gender == Gender.Female).FirstOrDefault();

            Assert.Equal("Marcus", man.Name);
            Assert.Equal("Bridgette", woman.Name);
            Assert.Equal(10, man.Age);
            Assert.Equal(20, woman.Age);
            Assert.Equal(@"6'7""", man.Height);
            Assert.Equal("brown", woman.HairColor);
        }
    }
}
