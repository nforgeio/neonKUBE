//-----------------------------------------------------------------------------
// FILE:	    GrpcModifyLocalHostsRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

using ProtoBuf.Grpc;

namespace Neon.Kube.GrpcProto.Desktop
{
    /// <summary>
    /// Modifies the local <b>$/etc/hosts</b> file which usually required elevated rights
    /// to access.  This request returns a <see cref="GrpcBaseReply"/>.  See
    /// <see cref="NetHelper.ModifyLocalHosts(string, Dictionary{string, System.Net.IPAddress})"/>
    /// for more information about how this works.
    /// </summary>
    [DataContract]
    public class GrpcModifyLocalHostsRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcModifyLocalHostsRequest()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="section">
        /// <para>
        /// Specifies the string to use to delimit the host names section.  This is required and
        /// must be a non-empty string consisting of up to 63 non-control ASCII characters.  Section
        /// names are case sensitive.
        /// </para>
        /// </param>
        /// <param name="hostEntries">A dictionary mapping the hostnames to an IP address or <c>null</c>.</param>
        /// <remarks>
        /// <note>
        /// This method requires elevated administrative privileges.
        /// </note>
        /// <para>
        /// This method adds or removes a temporary section of host entry definitions
        /// delimited by special comment lines.  When <paramref name="hostEntries"/> is 
        /// non-null and non-empty, the section will be added or updated.  Otherwise, the
        /// section will be removed.
        /// </para>
        /// <para>
        /// You can remove all host sections by passing both <paramref name="hostEntries"/> 
        /// and <paramref name="section"/> as <c>null</c>.
        /// </para>
        /// </remarks>
        public GrpcModifyLocalHostsRequest(string section, Dictionary<string, IPAddress>? hostEntries = null)
        {
            this.Section     = section;
            this.HostEntries = hostEntries;
        }

        /// <summary>
        /// Identifies the section.
        /// </summary>
        [DataMember(Order = 1)]
        public string? Section { get; set; }

        /// <summary>
        /// Optionally specifies the host entries to located within the target section.
        /// </summary>
        [DataMember(Order = 2)]
        public Dictionary<string, IPAddress>? HostEntries { get; set; }
    }
}
