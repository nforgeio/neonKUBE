//-----------------------------------------------------------------------------
// FILE:	    ServiceSecret.cs
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
    /// Service secret.
    /// </summary>
    public class ServiceSecret : INormalizable
    {
        /// <summary>
        /// The Docker secret ID.
        /// </summary>
        [JsonProperty(PropertyName = "SecretID", Required = Required.Always)]
        public string SecretId { get; set; }

        /// <summary>
        /// The secret name.
        /// </summary>
        [JsonProperty(PropertyName = "SecretName", Required = Required.Always)]
        public string SecretName { get; set; }

        /// <summary>
        /// Secret file information.
        /// </summary>
        [JsonProperty(PropertyName = "File", Required = Required.Always)]
        public ServiceFile File { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            File?.Normalize();
        }
    }
}
