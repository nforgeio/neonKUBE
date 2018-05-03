//-----------------------------------------------------------------------------
// FILE:	    ServicePrivileges.cs
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
    /// Security options for service containers.
    /// </summary>
    public class ServicePrivileges : INormalizable
    {
        /// <summary>
        /// <b>Windows Only:</b> Windows container credential specification.
        /// </summary>
        [JsonProperty(PropertyName = "CredentialSpec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceCredentialSpec CredentialSpec { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            CredentialSpec = CredentialSpec ?? new ServiceCredentialSpec();

            CredentialSpec?.Normalize();
        }
    }
}
