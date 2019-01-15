//-----------------------------------------------------------------------------
// FILE:	    ServiceGenericResources.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Describes user-defined resource settings.
    /// </summary>
    public class ServiceGenericResources : INormalizable
    {
        /// <summary>
        /// Named setting for a resource.
        /// </summary>
        [JsonProperty(PropertyName = "NamedResourceSpec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "NamedResourceSpec", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceNamedResourceSpec NamedResourceSpec { get; set; }

        /// <summary>
        /// Discrete setting for a resource.
        /// </summary>
        [JsonProperty(PropertyName = "DiscreteResourceSpec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "DiscreteResourceSpec", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceDiscreteResourceSpec DiscreteResourceSpec { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            // The presence or absence of the instance properties is
            // important, so we're not going to normalize them.
        }
    }
}
