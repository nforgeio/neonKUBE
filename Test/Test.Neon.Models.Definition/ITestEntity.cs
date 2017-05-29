//-----------------------------------------------------------------------------
// FILE:	    ITestEntity.cs
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;

namespace Test.Neon.Models
{
    [Entity(Type = "test.entity")]
    public interface ITestEntity
    {
        [EntityProperty(Name = "string")]
        string String { get; set; }

        [EntityProperty(Name = "int")]
        int Int { get; set; }

        [EntityProperty(Name = "guid")]
        Guid Guid { get; set; }

        [EntityProperty(Name = "child")]
        ITestEntity Child { get; set; }

        [EntityProperty(Name = "string_list")]
        string[] StringList { get; set; }

        [EntityProperty(Name = "child_list")]
        ITestEntity[] ChildList { get; set; }

        [EntityProperty(Name = "child_link", IsLink = true)]
        ITestEntity ChildLink { get; set; }

        [EntityProperty(Name = "link_list", IsLink = true)]
        ITestEntity[] LinkList { get; set; }

        [EntityProperty(Name = "doc_link")]
        ITestBinder DocLink { get; set; }

        [EntityProperty(Name = "doc_list")]
        ITestBinder[] DocList { get; set; }
    }
}
