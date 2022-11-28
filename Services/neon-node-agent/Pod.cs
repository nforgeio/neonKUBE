//-----------------------------------------------------------------------------
// FILE:	    Pod.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace NeonNodeAgent
{
    /// <summary>
    /// Abstracts access to the host pod properties.
    /// </summary>
    public static class Pod
    {
        /// <summary>
        /// Returns the Kubernetes namespace where the executing pod is running.
        /// </summary>
        public static readonly string Namespace = Environment.GetEnvironmentVariable("POD_NAMESPACE");

        /// <summary>
        /// Returns the name of the executing pod.
        /// </summary>
        public static readonly string Name = Environment.GetEnvironmentVariable("POD_NAME");

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Pod()
        {
            // Initializes these properties from environment variables when we're running 
            // in a cluster, otherwise configure test values when running on workstation.

            if (NeonHelper.IsDevWorkstation)
            {
                Namespace = "default";
                Name      = KubeHelper.GetEmulatedPodName("neon-node-agent");
            }
            else
            {
                Namespace = Environment.GetEnvironmentVariable("POD_NAMESPACE");
                Name      = Environment.GetEnvironmentVariable("POD_NAME");
            }
        }
    }
}
