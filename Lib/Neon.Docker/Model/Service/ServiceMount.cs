//-----------------------------------------------------------------------------
// FILE:	    ServiceMount.cs
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
    /// Service mount specification.
    /// </summary>
    public class ServiceMount : INormalizable
    {
        /// <summary>
        /// The mount type.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServiceMountType))]
        public ServiceMountType Type { get; set; }

        /// <summary>
        /// Specifies the external mount source
        /// </summary>
        [JsonProperty(PropertyName = "Source", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Source { get; set; }

        /// <summary>
        /// Specifies where the mount will appear within the service containers. 
        /// </summary>
        [JsonProperty(PropertyName = "Target", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Target { get; set; }

        /// <summary>
        /// Specifies whether the mount is to be read-only within the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "ReadOnly", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Specifies the mount consistency.
        /// </summary>
        [JsonProperty(PropertyName = "Consistency", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(ServiceMountConsistency.Consistent)]
        public ServiceMountConsistency Consistency { get; set; }

        /// <summary>
        /// Specifies the bind propagation mode.
        /// </summary>
        [JsonProperty(PropertyName = "BindPropagation", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(ServiceMountBindPropagation.RPrivate)]
        public ServiceMountBindPropagation BindPropagation { get; set; }

        /// <summary>
        /// Specifies the volume driver.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeDriver", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string VolumeDriver { get; set; }

        /// <summary>
        /// Specifies volume labels as <b>LABEL=VALUE</b> items.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeLabel", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> VolumeLabel { get; private set; }

        /// <summary>
        /// Enable populating the volume with data from the container target.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeNoCopy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool VolumeNoCopy { get; set; } = false;

        /// <summary>
        /// Volume driver options as <b>OPTION=VALUE</b> items.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeOpt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> VolumeOpt { get; private set; }

        /// <summary>
        /// Specifies the <b>tmpfs</b> size in bytes.  A value of zero means unlimited.
        /// </summary>
        [JsonProperty(PropertyName = "TmpfsSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(0)]
        public long TmpfsSize { get; set; } = 0;

        /// <summary>
        /// Specifies the <b>tmpfs</b> file mode.
        /// </summary>
        [JsonProperty(PropertyName = "TmpfsMode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue("1777")]
        public string TmpfsMode { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            VolumeLabel = VolumeLabel ?? new List<string>();
            VolumeOpt   = VolumeOpt ?? new List<string>();
        }
    }
}
