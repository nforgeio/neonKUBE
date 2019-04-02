//-----------------------------------------------------------------------------
// FILE:	    Test_DataModel.cs
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
using Neon.Data;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen.DataModel
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
        List<string> List { get; set; }
        Dictionary<string, int> Dictionary { get; set; }
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
        [HashSource]
        string ParentProperty { get; set; }
    }

    public interface DerivedModel : BaseModel
    {
        [HashSource]
        string ChildProperty { get; set; }
    }

    public interface DefaultValues
    {
        [HashSource]
        [DefaultValue("Joe Bloe")]
        string Name { get; set; }

        [HashSource]
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
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<EmptyData>();
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.EmptyData\"}", data.ToString());

                data = context.CreateDataWrapperFrom<EmptyData>(data.ToString());
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.EmptyData\"}", data.ToString());

                data = context.CreateDataWrapperFrom<EmptyData>(data.ToJObject());
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.EmptyData\"}", data.ToString());

                //-------------------------------------------------------------
                // Verify Equals():

                var value1 = context.CreateDataWrapperFrom<EmptyData>(data.ToJObject());
                var value2 = context.CreateDataWrapperFrom<EmptyData>(data.ToJObject());

                Assert.True(value1.Equals(value1));
                Assert.True(value1.Equals(value2));
                Assert.True(value2.Equals(value1));

                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));
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
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<NoSetter>();
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.NoSetter\"}", data.ToString());

                data = context.CreateDataWrapperFrom<NoSetter>(data.ToString());
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.NoSetter\"}", data.ToString());

                data = context.CreateDataWrapperFrom<NoSetter>(data.JObject);
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.NoSetter\"}", data.ToString());
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
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<NoGetter>();
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.NoGetter\"}", data.ToString());

                data = context.CreateDataWrapperFrom<NoGetter>(data.ToString());
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.NoGetter\"}", data.ToString());

                data = context.CreateDataWrapperFrom<NoGetter>(data.JObject);
                Assert.Equal("{\"__ET\":\"TestCodeGen.DataModel.NoGetter\"}", data.ToString());
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
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<SimpleData>();
                Assert.Equal("{\"Name\":null,\"Age\":0,\"Enum\":\"One\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}", data.ToString());
                Assert.Equal("{\r\n  \"Name\": null,\r\n  \"Age\": 0,\r\n  \"Enum\": \"One\",\r\n  \"__ET\": \"TestCodeGen.DataModel.SimpleData\"\r\n}", data.ToString(indented: true));

                data = context.CreateDataWrapper<SimpleData>();
                data["Name"] = "Jeff";
                data["Age"]  = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}", data.ToString());

                data = context.CreateDataWrapperFrom<SimpleData>("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}");
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}", data.ToString());

                var jObject = data.ToJObject();
                data = context.CreateDataWrapperFrom<SimpleData>(jObject);
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}", data.ToString());

                var jsonText = data.ToString(indented: false);
                data = context.CreateDataWrapperFrom<SimpleData>(jsonText);
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}", data.ToString());

                jsonText = data.ToString(indented: true);
                data = context.CreateDataWrapperFrom<SimpleData>(jsonText);
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}", data.ToString());

                //-------------------------------------------------------------
                // Verify Equals():

                var value1 = context.CreateDataWrapperFrom<SimpleData>("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}");
                var value2 = context.CreateDataWrapperFrom<SimpleData>("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\",\"__ET\":\"TestCodeGen.DataModel.SimpleData\"}");

                Assert.True(value1.Equals(value1));
                Assert.True(value1.Equals(value2));
                Assert.True(value2.Equals(value1));

                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));

                value2["Name"] = "Bob";

                Assert.True(value1.Equals(value1));

                Assert.False(value1.Equals(value2));
                Assert.False(value2.Equals(value1));
                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));
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
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<BasicTypes>();
                Assert.Equal("{\"Bool\":false,\"Byte\":0,\"SByte\":0,\"Short\":0,\"UShort\":0,\"Int\":0,\"UInt\":0,\"Long\":0,\"ULong\":0,\"Float\":0.0,\"Double\":0.0,\"Decimal\":0.0,\"String\":null,\"__ET\":\"TestCodeGen.DataModel.BasicTypes\"}", data.ToString());

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

                Assert.Equal("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\",\"__ET\":\"TestCodeGen.DataModel.BasicTypes\"}", data.ToString());

                var jsonText = data.ToString(indented: false);
                data = context.CreateDataWrapperFrom<BasicTypes>(jsonText);
                Assert.Equal("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\",\"__ET\":\"TestCodeGen.DataModel.BasicTypes\"}", data.ToString());

                jsonText = data.ToString(indented: true);
                data = context.CreateDataWrapperFrom<BasicTypes>(jsonText);
                Assert.Equal("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\",\"__ET\":\"TestCodeGen.DataModel.BasicTypes\"}", data.ToString());

                //-------------------------------------------------------------
                // Verify Equals():

                var value1 = context.CreateDataWrapperFrom<BasicTypes>("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\",\"__ET\":\"TestCodeGen.DataModel.BasicTypes\"}");
                var value2 = context.CreateDataWrapperFrom<BasicTypes>("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\",\"__ET\":\"TestCodeGen.DataModel.BasicTypes\"}");

                Assert.True(value1.Equals(value1));
                Assert.True(value1.Equals(value2));
                Assert.True(value2.Equals(value1));

                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));

                value2["String"] = "Bob";

                Assert.True(value1.Equals(value1));

                Assert.False(value1.Equals(value2));
                Assert.False(value2.Equals(value1));
                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));
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
                Assert.Equal("{\"List\":null,\"Dictionary\":null,\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null,\"__ET\":\"TestCodeGen.DataModel.ComplexData\"}", data.ToString());

                // Initialize the list and verify.

                data["List"] = new List<string>() { "item0", "item1" };

                Assert.Equal("{\"List\":[\"item0\",\"item1\"],\"Dictionary\":null,\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null,\"__ET\":\"TestCodeGen.DataModel.ComplexData\"}", data.ToString());

                // Initialize the dictionary and verify.

                data["Dictionary"] = new Dictionary<string, int>()
                {
                    { "zero", 0 },
                    { "one", 1 }
                };

                Assert.Equal("{\"List\":[\"item0\",\"item1\"],\"Dictionary\":{\"zero\":0,\"one\":1},\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null,\"__ET\":\"TestCodeGen.DataModel.ComplexData\"}", data.ToString());

                // Initialize the one dimensional array and verify.

                data["SingleArray"] = new int[] { 100, 200 };
                Assert.Equal("{\"List\":[\"item0\",\"item1\"],\"Dictionary\":{\"zero\":0,\"one\":1},\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":[100,200],\"DoubleArray\":null,\"__ET\":\"TestCodeGen.DataModel.ComplexData\"}", data.ToString());

                // Initialize the two dimensional array and verify.

                data["DoubleArray"] = new int[][]
                {
                    new int[] { 100, 200 },
                    new int[] { 300, 400 }
                };

                Assert.Equal("{\"List\":[\"item0\",\"item1\"],\"Dictionary\":{\"zero\":0,\"one\":1},\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":[100,200],\"DoubleArray\":[[100,200],[300,400]],\"__ET\":\"TestCodeGen.DataModel.ComplexData\"}", data.ToString());

                // Verify that a property with [JsonIgnore] is not persisted.

                data["IgnoreThis"] = 1000;
                Assert.DoesNotContain("IgnoreThis", data.ToString());

                //-------------------------------------------------------------
                // Verify Equals():

                var value1 = context.CreateDataWrapperFrom<ComplexData>("{\"List\":[\"zero\"],\"Dictionary\":null,\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null,\"__ET\":\"TestCodeGen.DataModel.ComplexData\"}");
                var value2 = context.CreateDataWrapperFrom<ComplexData>("{\"List\":[\"zero\"],\"Dictionary\":null,\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null,\"__ET\":\"TestCodeGen.DataModel.ComplexData\"}");

                Assert.True(value1.Equals(value1));
                Assert.True(value1.Equals(value2));
                Assert.True(value2.Equals(value1));

                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));

                value2 = context.CreateDataWrapperFrom<ComplexData>("{\"List\":[\"NOT-ZERO\"],\"Dictionary\":null,\"Enum1\":\"One\",\"Enum2\":\"three\",\"Simple\":null,\"SingleArray\":null,\"DoubleArray\":null,\"__ET\":\"TestCodeGen.DataModel.ComplexData\"}");

                Assert.True(value1.Equals(value1));

                Assert.False(value1.Equals(value2));
                Assert.False(value2.Equals(value1));
                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));
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
        public void Derived()
        {
            // Verify that data models that inherit from other data models work.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                // Verify that [BaseModel] by itself works.

                var baseData = context.CreateDataWrapper<BaseModel>();

                baseData.Save();
                baseData.Load(isDerived: false);
                Assert.Equal(baseData.EntityType, baseData["__ET"]);
                Assert.Null(baseData["ParentProperty"]);

                baseData["ParentProperty"] = "Hello World!";
                Assert.Equal("Hello World!", baseData["ParentProperty"]);

                baseData = context.CreateDataWrapperFrom<BaseModel>(baseData.ToString());
                Assert.Equal("Hello World!", baseData["ParentProperty"]);

                // Verify that [DerivedModel] works too.

                var derivedData = context.CreateDataWrapper<DerivedModel>();

                derivedData.Save();
                derivedData.Load();
                Assert.Equal(derivedData.EntityType, derivedData["__ET"]);
                Assert.Null(derivedData["ParentProperty"]);
                Assert.Null(derivedData["ChildProperty"]);

                derivedData["ParentProperty"] = "base";
                Assert.Equal("base", derivedData["ParentProperty"]);

                derivedData["ChildProperty"] = "derived";
                Assert.Equal("derived", derivedData["ChildProperty"]);

                var json = derivedData.ToString(indented: true);

                derivedData = context.CreateDataWrapperFrom<DerivedModel>(derivedData.ToString());
                Assert.Equal("base", derivedData["ParentProperty"]);
                Assert.Equal("derived", derivedData["ChildProperty"]);

                //-------------------------------------------------------------
                // Verify Equals():

                var value1 = context.CreateDataWrapperFrom<DerivedModel>("{\"ParentProperty\":\"BaseValue\",\"ChildProperty\":\"DerivedValue\",\"__ET\":\"TestCodeGen.DataModel.DerivedModel\"}");
                var value2 = context.CreateDataWrapperFrom<DerivedModel>("{\"ParentProperty\":\"BaseValue\",\"ChildProperty\":\"DerivedValue\",\"__ET\":\"TestCodeGen.DataModel.DerivedModel\"}");

                Assert.True(value1.Equals(value1));
                Assert.True(value1.Equals(value2));
                Assert.True(value2.Equals(value1));

                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));

                // Verify that a change to the base class property is detected.

                value1 = context.CreateDataWrapperFrom<DerivedModel>("{\"ParentProperty\":\"BaseValue\",\"ChildProperty\":\"DerivedValue\",\"__ET\":\"TestCodeGen.DataModel.DerivedModel\"}");
                value2 = context.CreateDataWrapperFrom<DerivedModel>("{\"ParentProperty\":\"DIFFERENT\",\"ChildProperty\":\"DerivedValue\",\"__ET\":\"TestCodeGen.DataModel.DerivedModel\"}");

                Assert.True(value1.Equals(value1));

                Assert.False(value1.Equals(value2));
                Assert.False(value2.Equals(value1));
                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));

                // Verify that a change to the derived class property is detected.

                value1 = context.CreateDataWrapperFrom<DerivedModel>("{\"ParentProperty\":\"BaseValue\",\"ChildProperty\":\"DerivedValue\",\"__ET\":\"TestCodeGen.DataModel.DerivedModel\"}");
                value2 = context.CreateDataWrapperFrom<DerivedModel>("{\"ParentProperty\":\"BaseValue\",\"ChildProperty\":\"DIFFERENT\",\"__ET\":\"TestCodeGen.DataModel.DerivedModel\"}");

                Assert.True(value1.Equals(value1));

                Assert.False(value1.Equals(value2));
                Assert.False(value2.Equals(value1));
                Assert.False(value1.Equals(null));
                Assert.False(value1.Equals("Hello World!"));

                //-------------------------------------------------------------
                // Verify that we can use [ToDerived<TResult>()] to create a derived instance
                // from the base type.  This also exercises [GeneratedEntityFactory] a bit.

                derivedData = context.CreateDataWrapper<DerivedModel>();

                derivedData["ParentProperty"] = "parent";
                derivedData["ChildProperty"]  = "child";

                baseData    = context.CreateDataWrapperFrom<BaseModel>(derivedData.ToString());
                derivedData = baseData.ToDerived<DerivedModel>();

                //Assert.Equal("parent", derivedData["ParentProperty"]);
                Assert.Equal("child", derivedData["ChildProperty"]);
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

                Assert.Equal("{\"Field3\":\"three\",\"Field4\":\"four\",\"Field1\":\"one\",\"Field2\":\"two\",\"__ET\":\"TestCodeGen.DataModel.OrderedProperties\"}", data.ToString());
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
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<CustomNamesModel>();
                Assert.Equal("{\"CustomString\":null,\"CustomInt\":0,\"__ET\":\"TestCodeGen.DataModel.CustomNamesModel\"}", data.ToString());

                data["String"] = "Hello World!";
                data["Int"]    = 1001;
                Assert.Equal("{\"CustomString\":\"Hello World!\",\"CustomInt\":1001,\"__ET\":\"TestCodeGen.DataModel.CustomNamesModel\"}", data.ToString());

                data = context.CreateDataWrapperFrom<CustomNamesModel>(data.ToString());
                Assert.Equal("{\"CustomString\":\"Hello World!\",\"CustomInt\":1001,\"__ET\":\"TestCodeGen.DataModel.CustomNamesModel\"}", data.ToString());
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
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<SerializationDefaultsModel>();

                // Verify that the instance starts out with the correct 
                // default property values.

                Assert.Equal("Ignore", data["Ignore"]);
                Assert.Equal("IgnoreAndPopulate", data["IgnoreAndPopulate"]);
                Assert.Equal("Include", data["Include"]);
                Assert.Equal("Populate", data["Populate"]);

                // Verify that we get the same output when serializing
                // the same data multple times (this wasn't working
                // early on).

                var serialized = data.ToString();

                Assert.Equal(serialized, data.ToString());
                Assert.Equal(serialized, data.ToString());

                // Verify that defaults serialize correctly.

                Assert.Equal("{\"Include\":\"Include\",\"Populate\":\"Populate\",\"__ET\":\"TestCodeGen.DataModel.SerializationDefaultsModel\"}", data.ToString());

                // Verify that defaults deserialize correctly.

                data = context.CreateDataWrapper<SerializationDefaultsModel>();
                data = context.CreateDataWrapperFrom<SerializationDefaultsModel>(data.ToString());

                Assert.Equal("Ignore", data["Ignore"]);
                Assert.Equal("IgnoreAndPopulate", data["IgnoreAndPopulate"]);
                Assert.Equal("Include", data["Include"]);
                Assert.Equal("Populate", data["Populate"]);

                // Verify that non-default values serialize/desearlize correctly.

                data = context.CreateDataWrapper<SerializationDefaultsModel>();

                data["Ignore"]            = "NotIgnore";
                data["IgnoreAndPopulate"] = "NotIgnoreAndPopulate";
                data["Include"]           = "NotInclude";
                data["Populate"]          = "NotPopulate";

                Assert.Equal("{\"Ignore\":\"NotIgnore\",\"IgnoreAndPopulate\":\"NotIgnoreAndPopulate\",\"Include\":\"NotInclude\",\"Populate\":\"NotPopulate\",\"__ET\":\"TestCodeGen.DataModel.SerializationDefaultsModel\"}", data.ToString());

                data = context.CreateDataWrapperFrom<SerializationDefaultsModel>(data.ToString());
                Assert.Equal("{\"Ignore\":\"NotIgnore\",\"IgnoreAndPopulate\":\"NotIgnoreAndPopulate\",\"Include\":\"NotInclude\",\"Populate\":\"NotPopulate\",\"__ET\":\"TestCodeGen.DataModel.SerializationDefaultsModel\"}", data.ToString());

                Assert.Equal("NotIgnore", data["Ignore"]);
                Assert.Equal("NotIgnoreAndPopulate", data["IgnoreAndPopulate"]);
                Assert.Equal("NotInclude", data["Include"]);
                Assert.Equal("NotPopulate", data["Populate"]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void EqualsOperator()
        {
            // Verify that the generated binary "==" operator works.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var value1 = context.CreateDataWrapper<SimpleData>();
                var value2 = context.CreateDataWrapper<SimpleData>();

                value1["Name"] = "Jeff";
                value1["Age"] = 58;
                value1["Enum"] = MyEnum1.One;

                value2["Name"] = "Jeff";
                value2["Age"] = 58;
                value2["Enum"] = MyEnum1.One;

                Assert.True(DataWrapper.Equals<SimpleData>(value1, value1));
                Assert.True(DataWrapper.Equals<SimpleData>(value1, value2));

                Assert.False(DataWrapper.Equals<SimpleData>(null, value2));
                Assert.False(DataWrapper.Equals<SimpleData>(value1, null));

                value2["Name"] = "Bob";
                Assert.False(DataWrapper.Equals<SimpleData>(value1, value2));
                Assert.False(DataWrapper.Equals<SimpleData>(value2, value1));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void NotEqualsOperator()
        {
            // Verify that the generated binary "==" operator works.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var value1 = context.CreateDataWrapper<SimpleData>();
                var value2 = context.CreateDataWrapper<SimpleData>();

                value1["Name"] = "Jeff";
                value1["Age"] = 58;
                value1["Enum"] = MyEnum1.One;

                value2["Name"] = "Jeff";
                value2["Age"] = 58;
                value2["Enum"] = MyEnum1.One;

                Assert.False(DataWrapper.NotEquals<SimpleData>(value1, value1));
                Assert.False(DataWrapper.NotEquals<SimpleData>(value1, value2));

                Assert.True(DataWrapper.NotEquals<SimpleData>(null, value2));
                Assert.True(DataWrapper.NotEquals<SimpleData>(value1, null));

                value2["Name"] = "Bob";
                Assert.True(DataWrapper.NotEquals<SimpleData>(value1, value2));
                Assert.True(DataWrapper.NotEquals<SimpleData>(value2, value1));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void HashCode()
        {
            // Verify that GetHashCode() works.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                // Verify that we see a [InvalidOperationException] when computing a hash
                // on a data model with no properties tagged by [HashSource].

                var emptyData = context.CreateDataWrapper<EmptyData>();

                Assert.Throws<InvalidOperationException>(() => emptyData.GetHashCode());

                // Verify that we compute HASH=0 for a data model with a
                // single tagged parameter set to NULL.

                var baseData = context.CreateDataWrapper<BaseModel>();

                Assert.Equal(0, baseData.GetHashCode());

                // Verify that we compute a non-zero HASH for a data model with a
                // single tagged parameter set to to a value.

                baseData = context.CreateDataWrapper<BaseModel>();
                baseData["ParentProperty"] = "Hello World!";
                Assert.NotEqual(0, baseData.GetHashCode());

                // Verify that we can compute a HASH code for a derived class
                // and that the hash includes properties from both the derived
                // and parent classes.

                var derivedData = context.CreateDataWrapper<DerivedModel>();
                derivedData["ChildProperty"] = "Goodbye World!";
                var derivedCode1 = derivedData.GetHashCode();
                Assert.NotEqual(0, derivedCode1);

                derivedData["ParentProperty"] = "Hello World!";
                var derivedCode2 = derivedData.GetHashCode();
                Assert.NotEqual(0, derivedCode2);
                Assert.NotEqual(derivedCode1, derivedCode2);

                // Verify that we can compute hash codes for models
                // with multiple hash source properties.

                var defaultValues = context.CreateDataWrapper<DefaultValues>();

                defaultValues["Name"] = null;
                defaultValues["Age"]  = 0;
                Assert.Equal(0, defaultValues.GetHashCode());

                defaultValues["Name"] = "JoeBob";
                defaultValues["Age"]  = 0;
                Assert.Equal("JoeBob".GetHashCode(), defaultValues.GetHashCode());

                defaultValues["Name"] = null;
                defaultValues["Age"]  = 67;
                Assert.Equal(67.GetHashCode(), defaultValues.GetHashCode());

                defaultValues["Name"] = "JoeBob";
                defaultValues["Age"]  = 67;
                Assert.Equal(67.GetHashCode() ^ "JoeBob".GetHashCode(), defaultValues.GetHashCode());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void DeepClone()
        {
            // Verify that DeepClone() works.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                // Verify for an empty class.

                var emptyData       = context.CreateDataWrapper<EmptyData>();
                var clonedEmptyData = emptyData.DeepClone();

                Assert.NotNull(clonedEmptyData);
                Assert.NotSame(emptyData, clonedEmptyData);
                Assert.Equal(emptyData, clonedEmptyData);

                // Verify for simple data.

                var simpleData = context.CreateDataWrapper<SimpleData>();

                simpleData["Name"] = "Joe Bloe";
                simpleData["Age"]  = 67;
                simpleData["Enum"] = MyEnum1.Two;

                var clonedSimpleData = simpleData.DeepClone();

                Assert.NotNull(clonedSimpleData);
                Assert.NotSame(simpleData, clonedSimpleData);
                Assert.Equal(simpleData, clonedSimpleData);

                // Verify for inherited data.

                var derivedData = context.CreateDataWrapper<DerivedModel>();

                derivedData["ParentProperty"] = "parent";
                derivedData["ChildProperty"]  = "child";

                var clonedDerivedData = derivedData.DeepClone();

                Assert.NotNull(derivedData);
                Assert.NotSame(derivedData, clonedDerivedData);
                Assert.Equal(derivedData, clonedDerivedData);

                // Verify for complex data and also ensure that we actually
                // made copies of reference properties.

                var complexData = context.CreateDataWrapper<ComplexData>();

                complexData["List"]        = new List<string>() { "zero", "one", "two" };
                complexData["Dictionary"]  = new Dictionary<string, int>() { { "zero", 1 }, { "one", 1 } };
                complexData["Enum1"]       = MyEnum1.One;
                complexData["Enum2"]       = MyEnum2.Three;
                complexData["Simple"]      = simpleData.__Instance;
                complexData["SingleArray"] = new int[] { 0, 1, 2, 3, 4 };
                complexData["DoubleArray"] = new int[][] { new int[] { 0, 1, 2 }, new int[] { 3, 4, 5 } };

                var clonedComplexData = complexData.DeepClone();

                Assert.NotNull(clonedComplexData);
                Assert.NotSame(complexData, clonedComplexData);
                Assert.Equal(complexData, clonedComplexData);
                Assert.NotSame(complexData["List"], clonedComplexData["List"]);
                Assert.NotSame(complexData["Dictionary"], clonedComplexData["Dictionary"]);
                Assert.NotSame(complexData["Simple"], clonedComplexData["Simple"]);
                Assert.NotSame(complexData["SingleArray"], clonedComplexData["SingleArray"]);
                Assert.NotSame(complexData["DoubleArray"], clonedComplexData["DoubleArray"]);
                Assert.NotSame(((int[][])complexData["DoubleArray"])[0], ((int[][])clonedComplexData["DoubleArray"])[0]);
                Assert.NotSame(((int[][])complexData["DoubleArray"])[1], ((int[][])clonedComplexData["DoubleArray"])[1]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void RoundTripUnknownProperties()
        {
            // Verify that persisted properties that were unknown
            // at compile time are still round-tripped successfuly.
            // We're going to test this by accessing the backing
            // [JObject].  We're also going to verify that these
            // unknown properties are included in the equality
            // tests.
            //
            // This requirement was the inspiration for this entire code
            // generation thing and it's funny that it took me this
            // line to actually test it.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                // Verify for an empty class.

                var emptyData = context.CreateDataWrapper<EmptyData>();

                emptyData.JObject["Unknown"] = "very tricky!";

                var deserialzedEmptyData = context.CreateDataWrapperFrom<EmptyData>(emptyData.ToString());

                Assert.Equal(emptyData, deserialzedEmptyData);
                Assert.Equal("very tricky!", deserialzedEmptyData.JObject["Unknown"]);

                deserialzedEmptyData.JObject["Unknown"] = "even trickier!";

                var jObject = EntitySerializationHelper.DeepClone(emptyData.JObject);

                jObject["__ET"] = typeof(DataWrapper).FullName;

                Assert.False(emptyData.Equals(deserialzedEmptyData));
                Assert.False(DataWrapper.Equals(emptyData, deserialzedEmptyData));
                Assert.True(DataWrapper.NotEquals<EmptyData>(emptyData, deserialzedEmptyData));

                // Verify for simple data.

                var simpleData = context.CreateDataWrapper<SimpleData>();

                jObject["__ET"]               = typeof(SimpleData).FullName;
                simpleData.JObject["Unknown"] = "very tricky!";

                var deserialzedSimpleData = context.CreateDataWrapperFrom<SimpleData>(jObject.ToString());

                jObject = EntitySerializationHelper.DeepClone(deserialzedSimpleData.JObject);

                Assert.Equal(simpleData, deserialzedSimpleData);
                Assert.Equal("very tricky!", deserialzedSimpleData.JObject["Unknown"]);

                deserialzedSimpleData.JObject["Unknown"] = "even trickier!";

                jObject = EntitySerializationHelper.DeepClone(deserialzedSimpleData.JObject);

                jObject["__ET"] = typeof(SimpleData).FullName;

                Assert.False(simpleData.Equals(deserialzedSimpleData));
                Assert.False(DataWrapper.Equals(simpleData, deserialzedSimpleData));
                Assert.True(DataWrapper.NotEquals<SimpleData>(simpleData, deserialzedSimpleData));
            }
        }
    }
}
