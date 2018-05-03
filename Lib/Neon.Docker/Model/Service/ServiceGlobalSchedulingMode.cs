//-----------------------------------------------------------------------------
// FILE:	    ServiceGlobalSchedulingMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Global scheduling mode options.
    /// </summary>
    public class ServiceGlobalSchedulingMode : INormalizable
    {
        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
