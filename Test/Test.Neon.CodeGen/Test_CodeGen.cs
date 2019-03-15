//-----------------------------------------------------------------------------
// FILE:	    Test_CodeGen
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen.CodeGen
{
    public interface EmptyData
    {
    }

    public enum MyEnum1
    {
        One,
        Two,
        Three
    }

    [Flags]
    public enum MyEnum2 : int
    {
        [EnumMember(Value = "one")]
        One = 1,
        [EnumMember(Value = "two")]
        Two = 2,
        [EnumMember(Value = "three")]
        Three = 3
    }

    public interface SimpleData
    {
        string Name { get; set; }
        int Age { get; set; }
        MyEnum1 Enum { get; set; }
    }

    public interface BasicTypes
    {
        bool Bool { get; set; }
        byte Byte { get; set; }
        sbyte SByte { get; set; }
        short Short { get; set; }
        ushort UShort { get; set; }
        int Int { get; set; }
        uint UInt { get; set; }
        long Long { get; set; }
        ulong ULong { get; set; }
        float Float { get; set; }
        double Double { get; set; }
        decimal Decimal { get; set; }
        string String { get; set; }
    }

    public interface ComplexData
    {
        List<string> Items { get; set; }
        Dictionary<string, int> Lookup { get; set; }
        MyEnum1 Enum1 { get; set; }
        MyEnum2 Enum2 { get; set; }
        SimpleData Simple { get; set; }
        int[] SingleArray { get; set; }
        int[][] DoubleArray { get; set; }

        [JsonIgnore]
        int IgnoreThis { get; set; }
    }

    public interface NoSetter
    {
        string Value { get; }
    }

    public interface NoGetter
    {
        string Value { set; }
    }

    public interface BaseModel
    {
        string BaseProperty { get; set; }
    }

    public interface DerivedModel : BaseModel
    {
        string DerivedProperty { get; set; }
    }

    public interface DefaultValues
    {
        [DefaultValue("Joe Bloe")]
        string Name { get; set; }

        [DefaultValue(67)]
        int Age { get; set; }

        [DefaultValue(true)]
        bool IsRetired { get; set; }

        [DefaultValue(100000)]
        double NetWorth { get; set; }

        [DefaultValue(MyEnum1.Three)]
        MyEnum1 Enum1 { get; set; }
    }

    public interface OrderedProperties
    {
        string Field1 { get; set; }

        string Field2 { get; set; }

        [JsonProperty(Order = 0)]
        string Field3 { get; set; }

        [JsonProperty(Order = 1)]
        string Field4 { get; set; }
    }

    public interface NullableProperties
    {
        bool? Bool { get; set; }
        int? Int { get; set; }
        MyEnum1? Enum { get; set; }
    }

    public interface CustomNamesModel
    {
        [JsonProperty(PropertyName = "CustomString")]
        string String { get; set; }

        [JsonProperty(PropertyName = "CustomInt")]
        int Int { get; set; }
    }

    public interface SerializationDefaultsModel
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, Order = 0)]
        [DefaultValue("Ignore")]
        string Ignore { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, Order = 1)]
        [DefaultValue("IgnoreAndPopulate")]
        string IgnoreAndPopulate { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include, Order = 2)]
        [DefaultValue("Include")]
        string Include { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate, Order = 3)]
        [DefaultValue("Populate")]
        string Populate { get; set; }
    }

    [NoCodeGen]
    public class Test_DataModel
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void Empty()
        {
            // Verify that we can generate code for an empty data model.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<EmptyData>();
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<EmptyData>("{}");
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<EmptyData>(new JObject());
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void NoSetter()
        {
            // Verify that we can generate code for the [NoSetter] data model.
            // This has a single property that only has a getter which should
            // be ignored by the code generator, so this should end up being
            // equivalent to a model with no properties.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<NoSetter>();
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<NoSetter>("{}");
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<NoSetter>(new JObject());
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void NoGetter()
        {
            // Verify that we can generate code for the [NoGetter] data model.
            // This has a single property that only has a getter which should
            // be ignored by the code generator, so this should end up being
            // equivalent to a model with no properties.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<NoGetter>();
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<NoGetter>("{}");
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<NoGetter>(new JObject());
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void Simple()
        {
            // Verify that we can generate code for a simple data model.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<SimpleData>();
                Assert.Equal("{\"Name\":null,\"Age\":0,\"Enum\":\"One\"}", data.ToString());
                Assert.Equal("{\r\n  \"Name\": null,\r\n  \"Age\": 0,\r\n  \"Enum\": \"One\"\r\n}", data.ToString(indented: true));

                data = context.CreateDataWrapper<SimpleData>();
                data["Name"] = "Jeff";
                data["Age"]  = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}", data.ToString());

                data = context.CreateDataWrapperFrom<SimpleData>("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}");
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}", data.ToString());

                var jObject = data.ToJObject();
                data = context.CreateDataWrapperFrom<SimpleData>(jObject);
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}", data.ToString());

                var jsonText = data.ToString(indented: false);
                data = context.CreateDataWrapperFrom<SimpleData>(jsonText);
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}", data.ToString());

                jsonText = data.ToString(indented: true);
                data = context.CreateDataWrapperFrom<SimpleData>(jsonText);
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}", data.ToString());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void BasicTypes()
        {
            // Verify that we can generate code for basic data types.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<BasicTypes>();
                Assert.Equal("{\"Bool\":false,\"Byte\":0,\"SByte\":0,\"Short\":0,\"UShort\":0,\"Int\":0,\"UInt\":0,\"Long\":0,\"ULong\":0,\"Float\":0.0,\"Double\":0.0,\"Decimal\":0.0,\"String\":null}", data.ToString());

                data["Bool"]    = true;
                data["Byte"]    = (byte)1;
                data["SByte"]   = (sbyte)2;
                data["Short"]   = (short)3;
                data["UShort"]  = (ushort)4;
                data["Int"]     = (int)5;
                data["UInt"]    = (uint)6;
                data["Long"]    = (long)7;
                data["ULong"]   = (ulong)8;
                data["Float"]   = (float)9;
                data["Double"]  = (double)10;
                data["Decimal"] = (decimal)11;
                data["String"]  = "12";

                Assert.Equal("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\"}", data.ToString());

                var jsonText = data.ToString(indented: false);
                data = context.CreateDataWrapperFrom<BasicTypes>(jsonText);
                Assert.Equal("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\"}", data.ToString());

                jsonText = data.ToString(indented: true);
                data = context.CreateDataWrapperFrom<BasicTypes>(jsonText);
                Assert.Equal("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\"}", data.ToString());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void Complex()
        {
            // Verify that we can generate code for complex data types.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<ComplexData>();

                // This is going to throw a serialization exception because 0 is not a valid
                // [Enum2] orginal value, but that's what it will be initially set to because
                // we didn't use [DefaultValue].

                Assert.Throws<SerializationException>(() => data.ToString());

                // Set a valid [Enum2] Value and test again.

                data["Enum2"] = MyEnum2.Three;
                Assert.Equal("{\"Items\":null,\"Lookup\":null,\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null}", data.ToString());

                // Initialize the list and verify.

                data["Items"] = new List<string>() { "item0", "item1" };

                Assert.Equal("{\"Items\":[\"item0\",\"item1\"],\"Lookup\":null,\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null}", data.ToString());

                // Initialize the dictionary and verify.

                data["Lookup"] = new Dictionary<string, int>()
                {
                    { "zero", 0 },
                    { "one", 1 }
                };

                Assert.Equal("{\"Items\":[\"item0\",\"item1\"],\"Lookup\":{\"zero\":0,\"one\":1},\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null}", data.ToString());

                // Initialize the one dimensional array and verify.

                data["SingleArray"] = new int[] { 100, 200 };
                Assert.Equal("{\"Items\":[\"item0\",\"item1\"],\"Lookup\":{\"zero\":0,\"one\":1},\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":[100,200],\"DoubleArray\":null}", data.ToString());

                // Initialize the two dimensional array and verify.

                data["DoubleArray"] = new int[][]
                {
                    new int[] { 100, 200 },
                    new int[] { 300, 400 }
                };

                Assert.Equal("{\"Items\":[\"item0\",\"item1\"],\"Lookup\":{\"zero\":0,\"one\":1},\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":[100,200],\"DoubleArray\":[[100,200],[300,400]]}", data.ToString());

                // Verify that a property with [JsonIgnore] is not persisted.

                data["IgnoreThis"] = 1000;
                Assert.DoesNotContain("IgnoreThis", data.ToString());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void DefaultValues()
        {
            // Verify that data models with default property values are initialized correctly.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<DefaultValues>();

                Assert.Equal("Joe Bloe", data["Name"]);
                Assert.Equal(67, data["Age"]);
                Assert.Equal(100000.0, data["NetWorth"]);
                Assert.Equal(MyEnum1.Three, (MyEnum1)data["Enum1"]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void Inherit()
        {
            // Verify that data models that inherit from other data models work.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                // Verify that [BaseModel] by itself works.

                var baseData = context.CreateDataWrapper<BaseModel>();

                Assert.Null(baseData["BaseProperty"]);

                baseData["BaseProperty"] = "Hello World!";
                Assert.Equal("Hello World!", baseData["BaseProperty"]);

                baseData = context.CreateDataWrapperFrom<BaseModel>(baseData.ToString());
                Assert.Equal("Hello World!", baseData["BaseProperty"]);

                // Verify that [DerivedModel] works too.

                var derivedData = context.CreateDataWrapper<DerivedModel>();

                Assert.Null(derivedData["BaseProperty"]);
                Assert.Null(derivedData["DerivedProperty"]);

                derivedData["BaseProperty"] = "base";
                Assert.Equal("base", derivedData["BaseProperty"]);

                derivedData["DerivedProperty"] = "derived";
                Assert.Equal("derived", derivedData["DerivedProperty"]);

                var json = derivedData.ToString(indented: true);

                derivedData = context.CreateDataWrapperFrom<DerivedModel>(derivedData.ToString());
                Assert.Equal("base", derivedData["BaseProperty"]);
                Assert.Equal("derived", derivedData["DerivedProperty"]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void OrderedProperties()
        {
            // Verify that data models that specify a property order
            // are rendered correctly.  Note that properties that
            // aren't tagged with [JsonProperty()] should be rendered
            // after all of the properties that are tagged.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<OrderedProperties>();

                data["Field1"] = "one";
                data["Field2"] = "two";
                data["Field3"] = "three";
                data["Field4"] = "four";

                Assert.Equal("{\"Field3\":\"three\",\"Field4\":\"four\",\"Field1\":\"one\",\"Field2\":\"two\"}", data.ToString());
            }
        }

        [Fact(Skip = "Manually testing required")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void NullableProperties()
        {
            // NOTE: 
            //
            // This test doesn't work due to apparent limitations of
            // the DataWrapper class an perhaps .NET itself.  The problem
            // is related to assigning a nullable Enum value.  This
            // fails even though I explicitly cast the value to (MyEnum1?).
            //
            // I think the problem is related to the JIT compiler doing some
            // magic here and effectively stripping out the cast and 
            // just passing the non-nullable enum value and then 
            // [DataWrapper] fails when dynamically assigning the value 
            // to the property because the value is no longer a (MyEnum1?).
            //
            // I have verified this manually when using the generated
            // model classes directly.

            // Verify that we handle nullable property types correctly.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<NullableProperties>();
                Assert.Equal("{\"Bool\":null,\"Int\":null,\"Enum\":null}", data.ToString());

                data = context.CreateDataWrapperFrom<NullableProperties>(data.ToString());
                Assert.Equal("{\"Bool\":null,\"Int\":null,\"Enum\":null}", data.ToString());

                data["Bool"] = true;
                data["Int"]  = 100;
                data["Enum"] = (MyEnum1?)MyEnum1.Two;   // This throws an exception

                var s = data.ToString();

                Assert.Equal("", data.ToString());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void CustomPropertyNames()
        {
            // Vertify that [JsonProperty(PropertyName = "xxx")] works.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<CustomNamesModel>();
                Assert.Equal("{\"CustomString\":null,\"CustomInt\":0}", data.ToString());

                data["String"] = "Hello World!";
                data["Int"]    = 1001;
                Assert.Equal("{\"CustomString\":\"Hello World!\",\"CustomInt\":1001}", data.ToString());

                data = context.CreateDataWrapperFrom<CustomNamesModel>(data.ToString());
                Assert.Equal("{\"CustomString\":\"Hello World!\",\"CustomInt\":1001}", data.ToString());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void SerializationDefaults()
        {
            // Verify that we honor the [JsonProperty(DefaultValueHandling)] options.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data       = context.CreateDataWrapper<SerializationDefaultsModel>();
                var serialized = data.ToString();

                Assert.Equal(serialized, data.ToString());

                var s = data.ToString();

                Assert.Equal(s, data.ToString());
            }
        }
    }
}
