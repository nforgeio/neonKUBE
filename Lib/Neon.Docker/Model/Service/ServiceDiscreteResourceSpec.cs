//-----------------------------------------------------------------------------
// FILE:	    ServiceDiscreteResourceSpec.cs
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
    /// Describes name-valued user-defined resource setting.
    /// </summary>
    public class ServiceDiscreteResourceSpec : INormalizable
    {
        /// <summary>
        /// Identifies the setting.
        /// </summary>
        [JsonProperty(PropertyName = "Kind", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Kind", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Kind { get; set; }

        /// <summary>
        /// The setting value <c>long</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Value", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Value", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long Value { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
