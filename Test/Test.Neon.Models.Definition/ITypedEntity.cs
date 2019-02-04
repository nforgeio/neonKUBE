//-----------------------------------------------------------------------------
// FILE:	    ITypedEntity.cs
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
using Neon.DynamicData;

namespace Test.Neon.Models
{
    [DynamicInclude]
    public enum TestEntityTypes
    {
        [EnumMember(Value = "test.entity1")]
        TestEntity1,

        TestEntity2,
        TestEntity3
    }

    [DynamicEntity(Type = TestEntityTypes.TestEntity1)]
    public interface IEnumTypedEntity1
    {
        [DynamicEntityProperty(Name = "name")]
        string Name { get; set; }

        [DynamicEntityProperty(Name = "age")]
        int Age { get; set; }
    }

    [DynamicEntity(Type = TestEntityTypes.TestEntity2)]
    public interface IEnumTypedEntity2
    {
        [DynamicEntityProperty(Name = "name")]
        string Name { get; set; }

        [DynamicEntityProperty(Name = "age")]
        int Age { get; set; }
    }

    [DynamicEntity(Type = TestEntityTypes.TestEntity3)]
    public interface IEnumTypedEntity3
    {
        [DynamicEntityProperty(Name = "name")]
        string Name { get; set; }

        [DynamicEntityProperty(Name = "age")]
        int Age { get; set; }

        [DynamicEntityProperty(IsTypeProperty = true)]
        TestEntityTypes Type { get; }

        TestEntityTypes Enum { get; set; }
        TestEntityTypes[] EnumArray { get; set; }
    }

    [DynamicEntity(Type = "string.entity1")]
    public interface IStringTypedEntity1
    {
        [DynamicEntityProperty(Name = "name")]
        string Name { get; set; }

        [DynamicEntityProperty(Name = "age")]
        int Age { get; set; }
    }

    [DynamicEntity(Type = "string.entity2")]
    public interface IStringTypedEntity2
    {
        [DynamicEntityProperty(Name = "name")]
        string Name { get; set; }

        [DynamicEntityProperty(Name = "age")]
        int Age { get; set; }
    }

    [DynamicEntity(Type = "string.entity3")]
    public interface IStringTypedEntity3
    {
        [DynamicEntityProperty(Name = "name")]
        string Name { get; set; }

        [DynamicEntityProperty(Name = "age")]
        int Age { get; set; }

        [DynamicEntityProperty(IsTypeProperty = true)]
        string Type { get; }
    }
}
