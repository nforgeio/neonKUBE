//-----------------------------------------------------------------------------
// FILE:	    Endpoint.cs
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
using System.ComponentModel;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// Endpoint defines a scrapeable endpoint serving Prometheus metrics.
    /// </summary>
    public class Endpoint
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Endpoint()
        {
        }

        /// <summary>
        /// Interval at which metrics should be scraped If not specified Prometheus' global scrape interval is used.
        /// </summary>
        [DefaultValue(null)]
        public string Interval { get; set; }

        /// <summary>
        /// HTTP path to scrape for metrics.
        /// </summary>
        [DefaultValue(null)]
        public string Path { get; set; }

        /// <summary>
        /// Timeout after which the scrape is ended If not specified, the Prometheus global scrape timeout is used 
        /// unless it is less than Interval in which the latter is used.
        /// </summary>
        [DefaultValue(null)]
        public string ScrapeTimeout { get; set; }

        /// <summary>
        /// Name or number of the target port of the Pod behind the Service, the port must be specified with container 
        /// port property. Mutually exclusive with port.
        /// </summary>
        [DefaultValue(null)]
        public int TargetPort { get; set; }

        /// <summary>
        /// Name of the service port this endpoint refers to. Mutually exclusive with targetPort.	
        /// </summary>
        [DefaultValue(null)]
        public string Port { get; set; }

        /// <summary>
        /// HTTP scheme to use for scraping.
        /// </summary>
        [DefaultValue(null)]
        public string Scheme { get; set; }

        /// <summary>
        /// Optional HTTP URL parameters
        /// </summary>
        [DefaultValue(null)]
        public string Params { get; set; }
    }
}
