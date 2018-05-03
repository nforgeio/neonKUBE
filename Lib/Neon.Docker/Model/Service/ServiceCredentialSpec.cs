//-----------------------------------------------------------------------------
// FILE:	    ServiceCredentialSpec.cs
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
    /// <b>Windows-only:</b> Specifies how Windows credentials are to be
    /// loaded for the container.
    /// </summary>
    public class ServiceCredentialSpec : INormalizable
    {
        /// <summary>
        /// Specifies the file on the Docker host with the credentials.
        /// </summary>
        [JsonProperty(PropertyName = "File", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string File { get; set; }

        /// <summary>
        /// Specifies the Windows registry location on the Docker host with the
        /// credentials.
        /// </summary>
        [JsonProperty(PropertyName = "Registry", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Registry { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            // The presence or abence of these properties is important so we're
            // not going to normalize them.
        }
    }
}
