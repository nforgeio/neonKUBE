//-----------------------------------------------------------------------------
// FILE:	    ITestEntity.cs
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.DynamicData;

namespace Test.Neon.Models
{
    [DynamicEntity(Type = "test.entity")]
    public interface ITestEntity
    {
        [DynamicEntityProperty(Name = "string")]
        string String { get; set; }

        [DynamicEntityProperty(Name = "int")]
        int Int { get; set; }

        [DynamicEntityProperty(Name = "guid")]
        Guid Guid { get; set; }

        [DynamicEntityProperty(Name = "child")]
        ITestEntity Child { get; set; }

        [DynamicEntityProperty(Name = "string_list")]
        string[] StringList { get; set; }

        [DynamicEntityProperty(Name = "child_list")]
        ITestEntity[] ChildList { get; set; }

        [DynamicEntityProperty(Name = "child_link", IsLink = true)]
        ITestEntity ChildLink { get; set; }

        [DynamicEntityProperty(Name = "link_list", IsLink = true)]
        ITestEntity[] LinkList { get; set; }

        [DynamicEntityProperty(Name = "doc_link")]
        ITestBinder DocLink { get; set; }

        [DynamicEntityProperty(Name = "doc_list")]
        ITestBinder[] DocList { get; set; }
    }
}
