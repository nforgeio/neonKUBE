//-----------------------------------------------------------------------------
// FILE:	    ServiceBindOptions.cs
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
    /// Mounted directory bind options.
    /// </summary>
    public class ServiceBindOptions : INormalizable
    {
        /// <summary>
        /// Named setting for a resource.
        /// </summary>
        [JsonProperty(PropertyName = "Propagation", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServiceMountBindPropagation))]
        public ServiceMountBindPropagation Propagation { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
