//-----------------------------------------------------------------------------
// FILE:	    HostingEnvironment.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the possible cluster hosting environments.
    /// </summary>
    public enum HostingEnvironment
    {
        /// <summary>
        /// Unknown or unspecified hosting environment.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

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
        /// Hosted on directly on pre-provisioned bare metal or virtual machines.
        /// </summary>
        [EnumMember(Value = "bare-metal")]
        BareMetal,
        
        /// <summary>
        /// Google Cloud Platform.
        /// </summary>
        [EnumMember(Value = "google")]
        Google,

        /// <summary>
        /// Microsoft Hyper-V hypervisor.
        /// </summary>
        [EnumMember(Value = "hyperv")]
        HyperV,

        /// <summary>
        /// Citrix XenServer hypervisor running on remote servers (typically
        /// for production purposes).
        /// </summary>
        [EnumMember(Value = "xenserver")]
        XenServer
    }
}
