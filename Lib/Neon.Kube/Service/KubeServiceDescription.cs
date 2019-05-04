//-----------------------------------------------------------------------------
// FILE:	    KubeServiceDescription.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.OpenApi.Models;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;

namespace Neon.Kube.Service
{
    /// <summary>
    /// Describes a <see cref="KubeService"/> or <see cref="AspNetKubeService"/>.
    /// </summary>
    public class KubeServiceDescription
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public KubeServiceDescription()
        {
        }

        /// <summary>
        /// The service name as deployed to Kubernetes.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// For <see cref="AspNetKubeService"/> services, this specifies the path
        /// prefix to prepended to URIs accessing this service.  This defaults to
        /// the empty string.
        /// </summary>
        public string PathPrefix { get; set; } = string.Empty;

        /// <summary>
        /// For <see cref="AspNetKubeService"/> services, this specifies the network
        /// port to be used for URIs accessing this service.  This defaults to <b>80</b>.
        /// </summary>
        public int Port { get; set; } = 80;

        /// <summary>
        /// For <see cref="AspNetKubeService"/> services, this is set to the 
        /// metadata used for Swagger related purposes.  This defaults to a
        /// new <see cref="OpenApiInfo"/> with all properties set to their
        /// defaults.
        /// </summary>
        public OpenApiInfo ApiInfo { get; set; } = new OpenApiInfo();
    }
}
