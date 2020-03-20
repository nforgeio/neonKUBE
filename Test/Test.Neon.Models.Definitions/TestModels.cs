//-----------------------------------------------------------------------------
// FILE:	    TestModels.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.ModelGen;
using Neon.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Test.Neon.Models.Definitions
{
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

    public enum Gender
    {
        [EnumMember(Value = "unspecified")]
        Unspecified = 0,

        [EnumMember(Value = "male")]
        Male = 1,

        [EnumMember(Value = "female")]
        Female = 2,

        [EnumMember(Value = "other")]
        Other = 3
    }

    public enum GenderNotCustom
    {
        Unspecified = 0,
        Male = 1,
        Female = 2,
        Other = 3
    }

    [Persistable]
    public interface Person
    {
        [PersistableKey]
        int Id { get; set; }
        string Name { get; set; }
        int Age { get; set; }
        Gender Gender { get; set; }
        byte[] Data { get; set; }
    }

    public interface EnumEntity
    {
        Gender Gender { get; set; }
    }

    public interface EnumNotCustomEntity
    {
        GenderNotCustom Gender { get; set; }
    }

    public interface DateTimeEntity
    {
        DateTime Timestamp { get; set; }
    }

    [Persistable]
    public interface City
    {
        [PersistableKey]
        string Name { get; set; }
        int Population { get; set; }
    }

    [Persistable]
    public interface Country
    {
        [PersistableKey]
        string Name { get; set; }
        int Population { get; set; }
    }

    [Persistable]
    [DataModel(Name = "custom-person")]
    public interface CustomPerson
    {
        [PersistableKey]
        [JsonProperty(PropertyName = "my-id")]
        int Id { get; set; }
        [JsonProperty(PropertyName = "my-name")]
        string Name { get; set; }
        [JsonProperty(PropertyName = "my-age")]
        int Age { get; set; }
        [JsonProperty(PropertyName = "my-gender")]
        Gender Gender { get; set; }
        [JsonProperty(PropertyName = "my-data")]
        byte[] Data { get; set; }
    }

    [DataModel(Name = "nonpersistable-person")]
    public interface NonPersistablePerson
    {
        [PersistableKey]
        [JsonProperty(PropertyName = "my-id")]
        int Id { get; set; }
        [JsonProperty(PropertyName = "my-name")]
        string Name { get; set; }
        [JsonProperty(PropertyName = "my-age")]
        int Age { get; set; }
        [JsonProperty(PropertyName = "my-gender")]
        Gender Gender { get; set; }
        [JsonProperty(PropertyName = "my-data")]
        byte[] Data { get; set; }
    }

    public enum MyEnum
    {
        Zero,
        One,
        Two,
        Three
    }

    [Persistable]
    [DataModel(Name = "Family")]
    public interface Family
    {
        [PersistableKey]
        [JsonProperty(PropertyName = "id")]
        int Id { get; set; }

        [JsonProperty(PropertyName = "mother")]
        Person Mother { get; set; }

        [JsonProperty(PropertyName = "father")]
        Person Father { get; set; }

        [JsonProperty(PropertyName = "baby")]
        Person Baby { get; set; }
    }

    [Persistable]
    [DataModel(Name = "TagColor")]
    public interface TagColor
    {
        [PersistableKey]
        string Id { get; set; }

        string Name { get; set; }
    }

    [ServiceModel]
    [Route("/TestAspNetFixture")]
    public interface TestAspNetFixtureController
    {
        [HttpGet]
        string GetString(string input);

        [HttpGet]
        bool GetBool(bool input);

        [HttpGet]
        int GetInt(int input);

        [HttpGet]
        double GetDouble(double input);

        [HttpGet]
        TimeSpan GetTimeSpan(TimeSpan timespan);

        [HttpGet]
        Version GetVersion(Version version);

        [HttpGet]
        [Route("person/{id}/{name}/{age}")]
        Person CreatePerson(int id, string name, int age, Gender gender);

        [HttpGet]
        [Route("nonpersistable-person/{id}/{name}/{age}")]
        NonPersistablePerson CreateNonPersisablePerson(int id, string name, int age, Gender gender);

        [HttpPut]
        Person IncrementAge([FromBody] Person person);

        [HttpGet]
        int DefaultInt(int value = 10);

        [HttpGet]
        bool DefaultBool(bool value = true);

        [HttpGet]
        double DefaultDouble(double value = 1.234);

        [HttpGet]
        string DefaultString(string value = "test");

        [HttpGet]
        MyEnum DefaultEnum(MyEnum value = MyEnum.Three);

        [HttpGet]
        [Route("GetOptionalStringViaHeader_Null")]
        string GetOptionalStringViaHeader_Null([FromHeader(Name = "X-Test")] string value = null);

        [HttpGet]
        [Route("GetOptionalStringViaHeader_Value")]
        string GetOptionalStringViaHeader_Value([FromHeader(Name = "X-Test")] string value = "Hello World!");

        [HttpGet]
        [Route("GetOptionalStringViaQuery_Null")]
        string GetOptionalStringViaQuery_Null([FromQuery] string value = null);

        [HttpGet]
        [Route("GetOptionalStringViaQuery_Value")]
        string GetOptionalStringViaQuery_Value([FromQuery] string value = "Hello World!");

        [HttpGet]
        [Route("GetOptionalEnumViaHeader")]
        MyEnum GetOptionalEnumViaHeader([FromHeader(Name = "X-Test")] MyEnum value = MyEnum.Three);

        [HttpGet]
        [Route("GetOptionalEnumViaQuery")]
        MyEnum GetOptionalEnumViaQuery([FromQuery] MyEnum value = MyEnum.Three);

        [HttpPut]
        [Route("GetOptionalEnumViaBody")]
        MyEnum GetOptionalEnumViaBody([FromBody] MyEnum value = MyEnum.Three);

        [HttpGet]
        [Route("GetOptionalDoubleViaHeader")]
        double GetOptionalDoubleViaHeader([FromHeader(Name = "X-Test")] double value = 1.234);

        [HttpGet]
        [Route("GetOptionalDoubleViaQuery")]
        double GetOptionalDoubleViaQuery([FromQuery] double value = 1.234);

        [HttpPut]
        [Route("GetOptionalDoubleViaBody")]
        double GetOptionalDoubleViaBody([FromBody] double value = 1.234);

        [HttpPut]
        [Route("GetOptionalStringViaBody")]
        string GetOptionalStringViaBody([FromBody] string value = "Hello World!");

        [HttpPut]
        [Route("GetStringList")]
        List<string> GetStringList([FromBody] List<string> value);

        [HttpPut]
        [Route("GetPersonList")]
        List<Person> GetPersonList([FromBody] List<Person> value);

        [HttpPut]
        [Route("GetPersonArray")]
        Person[] GetPersonArray([FromBody] Person[] value);

        //[HttpGet]
        //[Route("EchoDateTime")]
        //DateTime EchoDateTime([FromQuery] DateTime date);
    }

    [ServiceModel]
    [Route("/TestUxAspNetFixture")]
    public interface TestUxAspNetFixtureController
    {
        [HttpGet]
        string GetString(string input);

        [HttpGet]
        bool GetBool(bool input);

        [HttpGet]
        int GetInt(int input);

        [HttpGet]
        double GetDouble(double input);

        [HttpGet]
        TimeSpan GetTimeSpan(TimeSpan timespan);

        [HttpGet]
        Version GetVersion(Version version);

        [HttpGet]
        [Route("person/{id}/{name}/{age}")]
        Person CreatePerson(int id, string name, int age, Gender gender);

        [HttpPut]
        Person IncrementAge([FromBody] Person person);

        [HttpGet]
        int DefaultInt(int value = 10);

        [HttpGet]
        bool DefaultBool(bool value = true);

        [HttpGet]
        double DefaultDouble(double value = 1.234);

        [HttpGet]
        string DefaultString(string value = "test");

        [HttpGet]
        MyEnum DefaultEnum(MyEnum value = MyEnum.Three);

        [HttpGet]
        [Route("GetOptionalStringViaHeader_Null")]
        string GetOptionalStringViaHeader_Null([FromHeader(Name = "X-Test")] string value = null);

        [HttpGet]
        [Route("GetOptionalStringViaHeader_Value")]
        string GetOptionalStringViaHeader_Value([FromHeader(Name = "X-Test")] string value = "Hello World!");

        [HttpGet]
        [Route("GetOptionalStringViaQuery_Null")]
        string GetOptionalStringViaQuery_Null([FromQuery] string value = null);

        [HttpGet]
        [Route("GetOptionalStringViaQuery_Value")]
        string GetOptionalStringViaQuery_Value([FromQuery] string value = "Hello World!");

        [HttpGet]
        [Route("GetOptionalEnumViaHeader")]
        MyEnum GetOptionalEnumViaHeader([FromHeader(Name = "X-Test")] MyEnum value = MyEnum.Three);

        [HttpGet]
        [Route("GetOptionalEnumViaQuery")]
        MyEnum GetOptionalEnumViaQuery([FromQuery] MyEnum value = MyEnum.Three);

        [HttpGet]
        [Route("GetOptionalDoubleViaHeader")]
        double GetOptionalDoubleViaHeader([FromHeader(Name = "X-Test")] double value = 1.234);

        [HttpGet]
        [Route("GetOptionalDoubleViaQuery")]
        double GetOptionalDoubleViaQuery([FromQuery] double value = 1.234);

        [HttpPut]
        [Route("GetOptionalDoubleViaBody")]
        double GetOptionalDoubleViaBody([FromBody] double value = 1.234);

        [HttpPut]
        [Route("GetOptionalStringViaBody")]
        string GetOptionalStringViaBody([FromBody] string value = "Hello World!");

        [HttpPut]
        [Route("GetStringList")]
        List<string> GetStringList([FromBody] List<string> value);

        [HttpPut]
        [Route("GetPersonList")]
        List<Person> GetPersonList([FromBody] List<Person> value);

        [HttpPut]
        [Route("GetPersonArray")]
        Person[] GetPersonArray([FromBody] Person[] value);

        //[HttpGet]
        //[Route("EchoDateTime")]
        //DateTime EchoDateTime([FromQuery] DateTime date);
    }

    [ServiceModel]
    [Route("")]
    public interface VerifyController0
    {
    }

    [ServiceModel]
    [Route("/foo")]
    public interface VerifyController1
    {
        [HttpGet]
        [Route]
        void Hello();
    }

    /// <summary>
    /// Used for testing a service client composed of multiple controllers.
    /// </summary>
    [Target("Default")]
    [ServiceModel(name: "Composed", group: "User")]
    [Route("/api/v1/user")]
    public interface ComposedUserController
    {
        [HttpGet]
        [Route("{id}")]
        string Get(int id);

        [HttpGet]
        string[] List();
    }

    /// <summary>
    /// Used for testing a service client composed of multiple controllers.
    /// </summary>
    [Target("Default")]
    [ServiceModel(name: "Composed", group: "Delivery")]
    [Route("/api/v1/delivery")]
    public interface ComposedDeliveryController
    {
        [HttpGet]
        [Route("{id}")]
        string Get(int id);

        [HttpGet]
        string[] List();
    }

    /// <summary>
    /// Used for testing a service client composed of multiple controllers.
    /// </summary>
    [Target("Default")]
    [ServiceModel(name: "Composed")]
    [Route("/api/v1")]
    public interface ComposedController
    {
        [HttpGet]
        string GetVersion();
    }
}
