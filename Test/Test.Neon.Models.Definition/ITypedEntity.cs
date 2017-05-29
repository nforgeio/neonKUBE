//-----------------------------------------------------------------------------
// FILE:	    ITypedEntity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

#pragma warning disable 1591

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;

namespace Test.Neon.Models
{
    [Include]
    public enum TestEntityTypes
    {
        [EnumMember(Value = "test.entity1")]
        TestEntity1,

        TestEntity2,
        TestEntity3
    }

    [Entity(Type = TestEntityTypes.TestEntity1)]
    public interface IEnumTypedEntity1
    {
        [EntityProperty(Name = "name")]
        string Name { get; set; }

        [EntityProperty(Name = "age")]
        int Age { get; set; }
    }

    [Entity(Type = TestEntityTypes.TestEntity2)]
    public interface IEnumTypedEntity2
    {
        [EntityProperty(Name = "name")]
        string Name { get; set; }

        [EntityProperty(Name = "age")]
        int Age { get; set; }
    }

    [Entity(Type = TestEntityTypes.TestEntity3)]
    public interface IEnumTypedEntity3
    {
        [EntityProperty(Name = "name")]
        string Name { get; set; }

        [EntityProperty(Name = "age")]
        int Age { get; set; }

        [EntityProperty(IsTypeProperty = true)]
        TestEntityTypes Type { get; }

        TestEntityTypes Enum { get; set; }
        TestEntityTypes[] EnumArray { get; set; }
    }

    [Entity(Type = "string.entity1")]
    public interface IStringTypedEntity1
    {
        [EntityProperty(Name = "name")]
        string Name { get; set; }

        [EntityProperty(Name = "age")]
        int Age { get; set; }
    }

    [Entity(Type = "string.entity2")]
    public interface IStringTypedEntity2
    {
        [EntityProperty(Name = "name")]
        string Name { get; set; }

        [EntityProperty(Name = "age")]
        int Age { get; set; }
    }

    [Entity(Type = "string.entity3")]
    public interface IStringTypedEntity3
    {
        [EntityProperty(Name = "name")]
        string Name { get; set; }

        [EntityProperty(Name = "age")]
        int Age { get; set; }

        [EntityProperty(IsTypeProperty = true)]
        string Type { get; }
    }
}
