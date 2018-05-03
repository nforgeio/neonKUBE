//-----------------------------------------------------------------------------
// FILE:	    ServiceGenericResources.cs
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
    /// Describes user-defined resource settings.
    /// </summary>
    public class ServiceGenericResources : INormalizable
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceGenericResources()
        {
        }

        /// <summary>
        /// Named setting for a resource.
        /// </summary>
        [JsonProperty(PropertyName = "NamedResourceSpec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceNamedResourceSpec NamedResourceSpec { get; set; }

        /// <summary>
        /// Discrete setting for a resource.
        /// </summary>
        [JsonProperty(PropertyName = "DiscreteResourceSpec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceDiscreteResourceSpec DiscreteResourceSpec { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
