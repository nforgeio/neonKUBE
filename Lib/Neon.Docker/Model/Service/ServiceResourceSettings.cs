//-----------------------------------------------------------------------------
// FILE:	    ServiceResourceSettings.cs
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
    /// Describes system resource consumption settings.
    /// </summary>
    public class ServiceResourceSettings : INormalizable
    {
        /// <summary>
        /// CPU utilization expressed as billionths of a CPU.
        /// </summary>
        [JsonProperty(PropertyName = "NanoCPUs", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public long? NanoCPUs { get; set; }

        /// <summary>
        /// Memory utilization as bytes.
        /// </summary>
        [JsonProperty(PropertyName = "MemoryBytes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public long? MemoryBytes { get; set; }

        /// <summary>
        /// User-defined generic resource settings.
        /// </summary>
        [JsonProperty(PropertyName = "GenericResources", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServiceGenericResources> GenericResources { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            GenericResources = GenericResources ?? new List<ServiceGenericResources>();

            foreach (var item in GenericResources)
            {
                item?.Normalize();
            }
        }
    }
}
