//-----------------------------------------------------------------------------
// FILE:	    TestModels.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
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

    [Persisted]
    public interface Person
    {
        [PersistedKey]
        int Id { get; set; }
        string Name { get; set; }
        int Age { get; set; }
        byte[] Data { get; set; }
    }

    [Persisted]
    public interface City
    {
        [PersistedKey]
        string Name { get; set; }
        int Population { get; set; }
    }

    [Persisted]
    public interface Country
    {
        [PersistedKey]
        string Name { get; set; }
        int Population { get; set; }
    }

    [Persisted]
    [DataModel(Name = "custom-person")]
    public interface CustomPerson
    {
        [PersistedKey]
        [JsonProperty(PropertyName = "my-id")]
        int Id { get; set; }
        [JsonProperty(PropertyName = "my-name")]
        string Name { get; set; }
        [JsonProperty(PropertyName = "my-age")]
        int Age { get; set; }
        [JsonProperty(PropertyName = "my-data")]
        byte[] Data { get; set; }
    }
}
