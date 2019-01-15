//-----------------------------------------------------------------------------
// FILE:	    ServiceNamedResourceSpec.cs
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
    public class ServiceNamedResourceSpec : INormalizable
    {
        /// <summary>
        /// Identifies the setting.
        /// </summary>
        [JsonProperty(PropertyName = "Kind", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Kind")]
        [DefaultValue(null)]
        public string Kind { get; set; }

        /// <summary>
        /// The setting value string.
        /// </summary>
        [JsonProperty(PropertyName = "Value", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Value")]
        [DefaultValue(null)]
        public string Value { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
