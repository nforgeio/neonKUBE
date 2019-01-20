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
        private const string latestSupportedVersion = "1.13.2";

        private JsonClient                      jsonClient;
        private Dictionary<string, string>      ubuntuKubeAdmPackages;
        private Dictionary<string, string>      ubuntuKubeCtlPackages;
        private Dictionary<string, string>      ubuntuKubeletPackages;

        /// <summary>
        /// Constructor.
        /// </summary>
        public HeadendClient()
        {
            jsonClient = new JsonClient();

            // $hack(jeff.lill):
            //
            // We need to manually maintain the Kubernetes version to the
            // corresponding [kubectl], [kubeadm], and [kubelet] package
            // versions so we'll be able to install the correct versions.
            //
            // These versions were obtained by starting an Ubuntu server
            // and running these commands:
            //
            //      apt-get update && apt-get install -y apt-transport-https curl
            //      curl - s https://packages.cloud.google.com/apt/doc/apt-key.gpg | apt-key add -
            //      echo "deb https://apt.kubernetes.io/ kubernetes-xenial main" > /etc/apt/sources.list.d/kubernetes.list
            //
            //      apt-cache madison kubeadm
            //      apt-cache madison kubectl
            //      apt-cache madison kubelet
            //
            // The [apt-cache madison *] commands will list the package versions in the
            // second output table column.  You'll need to ensure that each supported
            // Kubernetes version has a package version defined for each component.
            //
            // NOTE: You may see more than one package version for a component,
            //       like [1.13.0-00] and [1.13.0-01].  You should choose the
            //       greatest one.

            ubuntuKubeAdmPackages = new Dictionary<string, string>()
            {
                { "1.13.0", "1.13.0-00" },
                { "1.13.1", "1.13.1-00" },
                { "1.13.2", "1.13.2-00" },
            };

            ubuntuKubeCtlPackages = new Dictionary<string, string>()
            {
                { "1.13.0", "1.13.0-00" },
                { "1.13.1", "1.13.1-00" },
                { "1.13.2", "1.13.2-00" },
            };

            ubuntuKubeletPackages = new Dictionary<string, string>
            {
                { "1.13.0", "1.13.0-00" },
                { "1.13.1", "1.13.1-00" },
                { "1.13.2", "1.13.2-00" },
            };
        }

        /// <summary>
        /// Returns information required for setting up a Kubernetes cluster.  This
        /// includes things like the URIs to be used for downloading the <b>kubectl</b>
        /// and <b>kubeadm</b> tools.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>A <see cref="KubeSetupInfo"/> with the information.</returns>
        public async Task<KubeSetupInfo> GetSetupInfoAsync(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            var kubeVersion = Version.Parse(latestSupportedVersion);

            if (!clusterDefinition.Kubernetes.Version.Equals("latest", StringComparison.InvariantCultureIgnoreCase))
            {
                kubeVersion = Version.Parse(clusterDefinition.Kubernetes.Version);
            }
           
            // $todo(jeff.lill): Hardcoded
            // $todo(jeff.lill): Verify Docker/Kubernetes version compatibility.

            return await Task.FromResult(
                new KubeSetupInfo()
                {
                    LinuxKubeCtlUri             = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/linux/amd64/kubectl",
                    LinuxKubeAdminUri           = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/linux/amd64/kubeadm",
                    LinuxKubeletUri             = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/linux/amd64/kubelet",

                    OsxKubeCtlUri               = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/bin/darwin/amd64/kubectl",
                    
                    WindowsKubeCtlUri           = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/bin/windows/amd64/kubectl.exe",

                    UbuntuDockerPackageUri      = "https://s3-us-west-2.amazonaws.com/neonforge/kube/docker.ce-18.06.1-ubuntu-bionic-stable-amd64.deb",
                    UbuntuKubeAdmPackageVersion = ubuntuKubeAdmPackages[kubeVersion.ToString()],
                    UbuntuKubeCtlPackageVersion = ubuntuKubeCtlPackages[kubeVersion.ToString()],
                    UbuntuKubeletPackageVersion = ubuntuKubeletPackages[kubeVersion.ToString()],
                });
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            jsonClient.Dispose();
        }
    }
}
