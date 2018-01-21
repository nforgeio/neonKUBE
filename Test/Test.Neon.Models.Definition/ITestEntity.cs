//-----------------------------------------------------------------------------
// FILE:	    ITestEntity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
