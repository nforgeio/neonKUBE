//-----------------------------------------------------------------------------
// FILE:	    AttachmentInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.Serialization;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Neon.Common;
using Neon.DynamicData;

namespace EntityGen
{
    /// <summary>
    /// Describes a binder document's attachment.
    /// </summary>
    public class AttachmentInfo
    {
        /// <summary>
        /// The binder document's property name for this attachment.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// The Couchbase Lite document attachment name.
        /// </summary>
        public string AttachmentName { get; set; }
    }
}
