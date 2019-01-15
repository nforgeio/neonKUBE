//-----------------------------------------------------------------------------
// FILE:	    HostingEnvironments.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the possible cluster hosting environments.
    /// </summary>
    public enum HostingEnvironments
    {
        /// <summary>
        /// Hosted on directly on pre-provisioned bare metal or virtual machines.
        /// </summary>
        [EnumMember(Value = "machine")]
        Machine = 0,

        /// <summary>
        /// Amazon Web Services.
        /// </summary>
        [EnumMember(Value = "aws")]
        Aws,

        /// <summary>
        /// Microsoft Azure.
        /// </summary>
        [EnumMember(Value = "azure")]
        Azure,

        /// <summary>
        /// Google Cloud Platform.
        /// </summary>
        [EnumMember(Value = "google")]
        Google,

        /// <summary>
        /// Microsoft Hyper-V hypervisor running on remote servers
        /// (typically for production purposes).
        /// </summary>
        [EnumMember(Value = "hyper-v")]
        HyperV,

        /// <summary>
        /// Microsoft Hyper-V hypervisor running on the local workstation
        /// (typically for development or test purposes).
        /// </summary>
        [EnumMember(Value = "hyper-v-local")]
        HyperVLocal,

        /// <summary>
        /// Citrix XenServer hypervisor running on remote servers (typically
        /// for production purposes).
        /// </summary>
        [EnumMember(Value = "xenserver")]
        XenServer,

        /// <summary>
        /// Unknown or unspecified hosting environment.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown
    }
}
