//-----------------------------------------------------------------------------
// FILE:	    HeadendClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;
using Neon.Tasks;

namespace Neon.Kube
{
    // $todo(jefflill):
    //
    // I'm just hardcoding this for now so that I can complete client 
    // side coding.  I'll flesh this out when I actually implement the
    // headend services.

    /// <summary>
    /// Provides access to neonKUBE headend services.
    /// </summary>
    public sealed class HeadendClient : IDisposable
    {
        private const string defaultKubeVersion          = "1.18.6";
        private const string defaultKubeDashboardVersion = "1.10.1";
        private const string defaultDockerVersion        = "docker.ce-18.06.1";
        private const string defaultHelmVersion          = "3.2.4";
        private const string defaultCalicoVersion        = "3.14";
        private const string defaultIstioVersion         = "1.6.4";

        private string[] supportedDockerVersions
            = new string[]
            {
                defaultDockerVersion
            };

        private string[] supportedHelmVersions
            = new string[]
            {
                defaultHelmVersion
            };

        private string[] supportedCalicoVersions
            = new string[]
            {
                defaultCalicoVersion
            };

        private static string[] supportedIstioVersions
            = new string[]
            {
                defaultIstioVersion
            };

        private JsonClient                      jsonClient;
        private JsonClient                      gitHubClient;
        private Dictionary<string, string>      ubuntuKubeAdmPackages;
        private Dictionary<string, string>      ubuntuKubeCtlPackages;
        private Dictionary<string, string>      ubuntuKubeletPackages;

        /// <summary>
        /// Constructor.
        /// </summary>
        public HeadendClient()
        {
            jsonClient = new JsonClient();
            jsonClient.HttpClient.Timeout = TimeSpan.FromSeconds(10);

            gitHubClient = new JsonClient();
            gitHubClient.BaseAddress = new Uri("https://raw.githubusercontent.com");
            gitHubClient.DefaultRequestHeaders.UserAgent.ParseAdd("HttpClient");
            
            // $hack(jefflill):
            //
            // We need to manually maintain the Kubernetes version to the
            // corresponding [kubectl], [kubeadm], and [kubelet] package
            // versions so we'll be able to install the correct versions.
            //
            // These versions were obtained by starting an Ubuntu server
            // and running these commands:
            //
            //      apt-get update && apt-get install -yq --allow-downgrades apt-transport-https curl
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
                { "1.13.3", "1.13.3-00" },
                { "1.14.1", "1.14.1-00" },
                { "1.15.0", "1.15.0-00" },
                { "1.15.4", "1.15.4-00" },
                { "1.16.0", "1.16.0-00" }
            };

            ubuntuKubeCtlPackages = new Dictionary<string, string>()
            {
                { "1.13.0", "1.13.0-00" },
                { "1.13.1", "1.13.1-00" },
                { "1.13.2", "1.13.2-00" },
                { "1.13.3", "1.13.3-00" },
                { "1.14.1", "1.14.1-00" },
                { "1.15.0", "1.15.0-00" },
                { "1.15.4", "1.15.4-00" },
                { "1.16.0", "1.16.0-00" }
            };

            ubuntuKubeletPackages = new Dictionary<string, string>
            {
                { "1.13.0", "1.13.0-00" },
                { "1.13.1", "1.13.1-00" },
                { "1.13.2", "1.13.2-00" },
                { "1.13.3", "1.13.3-00" },
                { "1.14.1", "1.14.1-00" },
                { "1.15.0", "1.15.0-00" },
                { "1.15.4", "1.15.4-00" },
                { "1.16.0", "1.16.0-00" }
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
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var kubeVersion = Version.Parse(defaultKubeVersion);

            if (!clusterDefinition.Kubernetes.Version.Equals("default", StringComparison.InvariantCultureIgnoreCase))
            {
                kubeVersion = Version.Parse(clusterDefinition.Kubernetes.Version);
            }

            var kubeDashboardVersion = Version.Parse(defaultKubeDashboardVersion);

            if (!clusterDefinition.Kubernetes.DashboardVersion.Equals("default", StringComparison.InvariantCultureIgnoreCase))
            {
                kubeDashboardVersion = Version.Parse(clusterDefinition.Kubernetes.DashboardVersion);
            }

            // Ensure that the we have package versions defined for the selected Kubernetes version.

            Covenant.Assert(ubuntuKubeAdmPackages.ContainsKey(kubeVersion.ToString()));
            Covenant.Assert(ubuntuKubeCtlPackages.ContainsKey(kubeVersion.ToString()));
            Covenant.Assert(ubuntuKubeletPackages.ContainsKey(kubeVersion.ToString()));

            // $todo(jefflill): Hardcoded
            // $todo(jefflill): Verify Docker/Kubernetes version compatibility.

            var dockerVersion = clusterDefinition.Docker.Version;

            if (dockerVersion == "default")
            {
                dockerVersion = defaultDockerVersion;
            }

            if (supportedDockerVersions.SingleOrDefault(v => v == dockerVersion) == null)
            {
                throw new KubeException($"[{dockerVersion}] is not a supported Docker version.");
            }

            var helmVersion = clusterDefinition.Kubernetes.HelmVersion;

            if (helmVersion == "default")
            {
                helmVersion = defaultHelmVersion;
            }

            if (supportedHelmVersions.SingleOrDefault(v => v == helmVersion) == null)
            {
                throw new KubeException($"[{helmVersion}] is not a supported Helm version.");
            }

            // $todo(jefflill):
            //
            // The code below supports only the Calico CNI for now.  This will probably be
            // replaced by the integrated Istio CNI soon.

            if (clusterDefinition.Network.Cni != NetworkCni.Calico)
            {
                throw new NotImplementedException($"The [{clusterDefinition.Network.Cni}] CNI is not currently supported.");
            }

            var calicoVersion = clusterDefinition.Network.CniVersion;

            if (calicoVersion == "default")
            {
                calicoVersion = defaultCalicoVersion;
            }

            if (clusterDefinition.Network.Cni == NetworkCni.Calico && supportedCalicoVersions.SingleOrDefault(v => v == calicoVersion) == null)
            {
                throw new KubeException($"[{calicoVersion}] is not a supported Calico version.");
            }

            var istioVersion = clusterDefinition.Network.IstioVersion;

            if (istioVersion == "default")
            {
                istioVersion = defaultIstioVersion;
            }

            if (supportedIstioVersions.SingleOrDefault(v => v == istioVersion) == null)
            {
                throw new KubeException($"[{istioVersion}] is not a supported Istio version.");
            }

            var setupInfo = new KubeSetupInfo()
            {
                KubeAdmLinuxUri             = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/linux/amd64/kubeadm",
                KubeCtlLinuxUri             = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/linux/amd64/kubectl",
                KubeletLinuxUri             = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/linux/amd64/kubelet",

                KubeCtlOsxUri               = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/bin/darwin/amd64/kubectl",
                    
                KubeCtlWindowsUri           = $"https://storage.googleapis.com/kubernetes-release/release/v{kubeVersion}/bin/windows/amd64/kubectl.exe",

                DockerPackageUbuntuUri      = $"https://s3-us-west-2.amazonaws.com/neonforge/kube/{dockerVersion}-ubuntu-bionic-stable-amd64.deb",
                KubeAdmPackageUbuntuVersion = ubuntuKubeAdmPackages[kubeVersion.ToString()],
                KubeCtlPackageUbuntuVersion = ubuntuKubeCtlPackages[kubeVersion.ToString()],
                KubeletPackageUbuntuVersion = ubuntuKubeletPackages[kubeVersion.ToString()],

                HelmLinuxUri                = $"https://storage.googleapis.com/kubernetes-helm/helm-v{helmVersion}-linux-amd64.tar.gz",
                HelmOsxUri                  = $"https://storage.googleapis.com/kubernetes-helm/helm-v{helmVersion}-darwin-amd64.tar.gz",
                HelmWindowsUri              = $"https://storage.googleapis.com/kubernetes-helm/helm-v{helmVersion}-windows-amd64.zip",

                // $todo(jefflill):
                //
                // I'm a little worried about where the "1.7" in the [CalicoSetupUri] came from.  I suspect that
                // this will vary too.  Once the Istio CNI is stable, we'll probably delete this anyway but if 
                // we do decide to allow for custom CNIs, we'll need to figure this out.

                CalicoRbacYamlUri           = $"https://docs.projectcalico.org/v{calicoVersion}/getting-started/kubernetes/installation/hosted/rbac-kdd.yaml",
                CalicoSetupYamlUri          = $"https://docs.projectcalico.org/v{calicoVersion}/manifests/calico.yaml",

                IstioLinuxUri               = $"https://github.com/istio/istio/releases/download/{istioVersion}/istio-{istioVersion}-linux.tar.gz"
            };

            var kubeVersionString                  = kubeVersion.ToString();

            setupInfo.Versions.Kubernetes          = kubeVersionString;

            var kubeDashboardConfig                = KubeDashboardConfig.GetDashboardConfigFor(kubeVersion.ToString());

            setupInfo.KubeDashboardYaml            = kubeDashboardConfig.ConfigYaml;
            setupInfo.Versions.KubernetesDashboard = kubeDashboardConfig.Version;

            setupInfo.Versions.Docker              = dockerVersion;
            setupInfo.Versions.Helm                = helmVersion;
            setupInfo.Versions.Calico              = calicoVersion;
            setupInfo.Versions.Istio               = istioVersion;

            await Task.CompletedTask;

            return setupInfo;
        }

        /// <summary>
        /// Returns client related information such as the location of the help and GitHub
        /// repo pages and the availability of updates.
        /// </summary>
        /// <returns>A <see cref="KubeClientInfo"/>.</returns>
        public async Task<KubeClientInfo> GetClientInfoAsync()
        {
            await SyncContext.ClearAsync;

            return new KubeClientInfo()
            {
                GitHubUrl             = "https://github.com/nforgeio/neonKUBE",
                HelpUrl               = "https://github.com/nforgeio/neonKUBE",
                ReleaseNotesUrl       = "https://github.com/nforgeio/neonKUBE",
                UpdateReleaseNotesUrl = null,
                UpdateUrl             = null
            };
        }

        /// <summary>
        /// Gets a helm chart from the NeonKube repository and returns it as a zip file
        /// </summary>
        /// <param name="chartName">The Helm chart name.</param>
        /// <param name="branch">The branch to get the chart from. Defaults to master.</param>
        /// <returns>The ZIP file encoded into bytes.</returns>
        public async Task<byte[]> GetHelmChartZipAsync(string chartName, string branch = "master")
        {
            await SyncContext.ClearAsync;

            using (var memoryStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    using (StreamReader reader = new StreamReader(
                        await (await gitHubClient.HttpClient.GetAsync($"nforgeio/neonKUBE/{branch}/Charts/tree.txt")).Content.ReadAsStreamAsync()))
                    {
                        var tree = reader.ReadToEnd();
                        foreach (var line in tree.Split('\n'))
                        {
                            if (line.Split('/')[0] == chartName)
                            {
                                if (Regex.Matches(tree, Regex.Escape(line.Trim())).Count > 1)
                                {
                                    continue;
                                }
                                var fileBytes = zip.CreateEntry(line.Replace($"{chartName}/", ""));

                                using (var entryStream = fileBytes.Open())
                                {
                                    var f = line.Trim();
                                    await entryStream.WriteAsync(await gitHubClient.HttpClient.GetByteArrayAsync($"nforgeio/neonKUBE/{branch}/Charts/{f}"));
                                }
                            }
                        }
                    }
                }
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Adds GitHub files from a directory to a ZIP archive.
        /// </summary>
        /// <param name="zip">The zip file.</param>
        /// <param name="directory">The directory to add.</param>
        /// <param name="baseDirectory">The base directory.</param>
        /// <returns>The <see cref="ZipArchive"/>.</returns>
        public async Task<ZipArchive> AddGitFilesToZipAsync(ZipArchive zip, string directory, string baseDirectory)
        {
            await SyncContext.ClearAsync;

            var dirListing = await gitHubClient.GetAsync<dynamic>(directory);

            foreach (var file in dirListing)
            {
                if (file.type == "file")
                {
                    var fileBytes = zip.CreateEntry(((string)file.path).Replace($"{baseDirectory}/", ""));

                    using (var entryStream = fileBytes.Open())
                    {
                        await entryStream.WriteAsync(await gitHubClient.HttpClient.GetByteArrayAsync((string)file.download_url));
                    }
                }
                else if (file.type == "dir")
                {
                    await AddGitFilesToZipAsync(zip, (string)file.url, baseDirectory);
                }
            }

            return zip;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            jsonClient.Dispose();
        }
    }
}
