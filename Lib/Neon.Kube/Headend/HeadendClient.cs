//-----------------------------------------------------------------------------
// FILE:	    HeadendClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    // $todo(jeff.lill):
    //
    // I'm just hardcoding this for now so that I can complete client 
    // side coding.  I'll flesh this out when I actually implement the
    // headend services.

    /// <summary>
    /// Provides access to neonHIVE headend services.
    /// </summary>
    public sealed class HeadendClient : IDisposable
    {
        private JsonClient jsonClient;

        /// <summary>
        /// Constructor.
        /// </summary>
        public HeadendClient()
        {
            jsonClient = new JsonClient();
        }

        /// <summary>
        /// Returns information required for setting up a Kubernetes cluster.  This
        /// includes things like the URIs to be used for downloading the <b>kubectl</b>
        /// and <b>kubeadm</b> tools.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>A <see cref="KubeSetupInfo"/> with the information.</returns>
        public KubeSetupInfo GetSetupInfo(ClusterDefinition clusterDefinition)
        {
            // $todo(jeff.lill): Hardcoded
            // $todo(jeff.lill): Verify Docker/Kubernetes version compatibility.

            return new KubeSetupInfo()
            {
                KubeAdminUri     = $"",
                KubeCtlUri       = $"",
                DockerPackageUri = "https://s3-us-west-2.amazonaws.com/neonforge/kube/docker.ce-18.06.1-ubuntu-bionic-stable-amd64.deb"
            };
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            jsonClient.Dispose();
        }
    }
}
