//-----------------------------------------------------------------------------
// FILE:	    ServiceTmpfsOptions.cs
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
    /// Volume Tempfs options.
    /// </summary>
    public class ServiceTmpfsOptions : INormalizable
    {
        /// <summary>
        /// Specifies the <b>tmpfs</b> size in bytes.
        /// </summary>
        [JsonProperty(PropertyName = "SizeBytes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public long? SizeBytes { get; set; } = 0;

        /// <summary>
        /// Specifies the <b>tmpfs</b> file permission mode encoded as an integer.
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(1023)]    // 1777 Linux octal file mode converted to decimal
        public int Mode { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
