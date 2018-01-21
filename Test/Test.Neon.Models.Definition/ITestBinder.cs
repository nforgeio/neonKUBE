//-----------------------------------------------------------------------------
// FILE:	    ITestBinder.cs
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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.DynamicData;

namespace Test.Neon.Models
{
    [BinderDocument(typeof(ITestEntity))]
    public interface ITestBinder
    {
        [BinderAttachment]
        string TestImage1 { get; }

        [BinderAttachment(AttachmentName = "test_image2")]
        string TestImage2 { get; }
    }
}
