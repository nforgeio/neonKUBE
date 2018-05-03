//-----------------------------------------------------------------------------
// FILE:	    ServiceDiscreteResourceSpec.cs
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
    /// Describes name-valued user-defined resource setting.
    /// </summary>
    public class ServiceDiscreteResourceSpec : INormalizable
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceDiscreteResourceSpec()
        {
        }

        /// <summary>
        /// Identifies the setting.
        /// </summary>
        [JsonProperty(PropertyName = "Kind", Required = Required.Always)]
        public string Kind { get; set; }

        /// <summary>
        /// The setting value <c>long</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Value", Required = Required.Always)]
        public long Value { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
