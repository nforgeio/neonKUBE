//-----------------------------------------------------------------------------
// FILE:	    GrpcLocalHostSection.cs
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
using System.Diagnostics.Contracts;
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
    /// Holds information about a host section from the local <b>$/etc/hosts</b> file
    /// as returned for a <see cref="GrpcListLocalHostsSectionsRequest"/> within a
    /// <see cref="GrpcListLocalHostsSectionsReply"/>.
    /// </summary>
    public class GrpcLocalHostSection
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcLocalHostSection()
        {
        }

        /// <summary>
        /// Constructs an instance using the sections passed.
        /// </summary>
        /// <param name="section">The <see cref="LocalHostSection"/> instance being wrapped.</param>
        public GrpcLocalHostSection(LocalHostSection? section)
        {
            Covenant.Requires<ArgumentNullException>(section != null, nameof(section));

            if (section == null)
            {
                return;
            }

            this.Name    = section.Name;
            this.HostEntries = new Dictionary<string, IPAddress>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var item in section.HostEntries)
            {
                this.HostEntries[item.Key] = item.Value;
            }
        }

        /// <summary>
        /// The host section name.
        /// </summary>
        [DataMember(Order = 1)]
        public string? Name { get; set; }

        /// <summary>
        /// The map of hostnames to IP addresses.
        /// </summary>
        [DataMember(Order = 2)]
        public Dictionary<string, IPAddress>? HostEntries { get; set; }

        /// <summary>
        /// Converts this <see cref="GrpcLocalHostSection"/> into a <see cref="LocalHostSection"/>.
        /// </summary>
        /// <returns>The converted <see cref="LocalHostSection"/>.</returns>
        public LocalHostSection ToLocalHostSection()
        {
            return new LocalHostSection(this.Name, this.HostEntries);
        }
    }
}
