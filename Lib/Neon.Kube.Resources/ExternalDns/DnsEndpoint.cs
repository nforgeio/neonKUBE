//-----------------------------------------------------------------------------
// FILE:	    DnsEndpoint.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Endpoint is a high-level way of a connection between a
    /// service and an IP.
    /// </summary>
    public class DnsEndpoint
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DnsEndpoint()
        {
        }

        /// <summary>
        /// The hostname of the DNS record.
        /// </summary>
        public string DnsName { get; set; }

        /// <summary>
        /// Labels defined for the Endpoint.
        /// </summary>
        public Dictionary<string, string> Labels { get; set; }

        /// <summary>
        /// Stores provider specific config.
        /// </summary>
        public Dictionary<string, object> ProviderSpecific { get; set; }

        /// <summary>
        /// TTL for the record.
        /// </summary>
        public int RecordTTL { get; set; }

        /// <summary>
        /// The <see cref="RecordType"/>
        /// </summary>
        public DnsRecordType RecordType { get; set; }

        /// <summary>
        /// The targets the DNS record points to.
        /// </summary>
        public List<string> Targets { get; set; }

        /// <summary>
        /// Identifier to distinguish multiple records with the
        /// same name and type(e.g.Route53 records with routing
        /// policies other than 'simple')
        /// </summary>
        public string SetIdentifier { get; set; }
    }
}
