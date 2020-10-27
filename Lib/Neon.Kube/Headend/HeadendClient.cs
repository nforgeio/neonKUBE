//-----------------------------------------------------------------------------
// FILE:	    HeadendClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using Couchbase.Configuration.Server.Serialization;

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
        private JsonClient      jsonClient;
        private JsonClient      gitHubClient;

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

            // Determine the node template URI.
            // $todo(jefflill): Hardcoded

            var linuxTemplateUri = (string)null;

            switch (clusterDefinition.Hosting.Environment)
            {
                case HostingEnvironment.Aws:
                case HostingEnvironment.Azure:
                case HostingEnvironment.Google:

                    // Node templates are provided by virtual machine images in the cloud.

                    linuxTemplateUri = null;
                    break;

                case HostingEnvironment.BareMetal:

                    break;

                case HostingEnvironment.HyperV:
                case HostingEnvironment.HyperVLocal:

                    if (!string.IsNullOrEmpty(clusterDefinition.LinuxTemplateUri))
                    {
                        linuxTemplateUri = clusterDefinition.LinuxTemplateUri;
                    }
                    else
                    {
                        linuxTemplateUri = $"https://s3-us-west-2.amazonaws.com/neonkube/vm-images/hyperv/neon-{clusterDefinition.LinuxDistribution}-{clusterDefinition.LinuxVersion}.vhdx";
                    }
                    break;

                case HostingEnvironment.XenServer:

                    if (!string.IsNullOrEmpty(clusterDefinition.LinuxTemplateUri))
                    {
                        linuxTemplateUri = clusterDefinition.LinuxTemplateUri;
                    }
                    else
                    {
                        // $todo(jefflill): 
                        //
                        // XenServer/XCP-ng doesn't support HTTPS!  This seems super odd.  Perhaps they
                        // don't want to update public root certificates or something.  This is a security
                        // hole.  We need to investigate this.
                        //
                        //      https://github.com/nforgeio/neonKUBE/issues/971

                        linuxTemplateUri = $"http://s3-us-west-2.amazonaws.com/neonkube/vm-images/xenserver/neon-{clusterDefinition.LinuxDistribution}-{clusterDefinition.LinuxVersion}.xva";
                    }
                    break;

                default:

                    throw new NotImplementedException($"Hosting environment [{clusterDefinition.Hosting.Environment}] is not implemented.");
            }

            var setupInfo = new KubeSetupInfo()
            {
                LinuxTemplateUri   = linuxTemplateUri,

                KubeAdmLinuxUri    = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubeadm",
                KubeCtlLinuxUri    = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubectl",
                KubeletLinuxUri    = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubelet",

                KubeCtlOsxUri      = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/bin/darwin/amd64/kubectl",
                    
                KubeCtlWindowsUri  = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/bin/windows/amd64/kubectl.exe",

                DockerPackageUri   = $"https://neonkube.s3-us-west-2.amazonaws.com/docker/{KubeVersions.DockerVersion}-ubuntu-focal-stable_amd64.deb",

                HelmLinuxUri       = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-linux-amd64.tar.gz",
                HelmOsxUri         = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-darwin-amd64.tar.gz",
                HelmWindowsUri     = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-windows-amd64.zip",

                CalicoRbacYamlUri  = $"https://docs.projectcalico.org/v{KubeVersions.CalicoVersion}/getting-started/kubernetes/installation/hosted/rbac-kdd.yaml",
                CalicoSetupYamlUri = $"https://docs.projectcalico.org/v{KubeVersions.CalicoVersion}/manifests/calico.yaml",

                IstioLinuxUri      = $"https://github.com/istio/istio/releases/download/{KubeVersions.IstioVersion}/istioctl-{KubeVersions.IstioVersion}-linux-amd64.tar.gz"
            };

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
                    using (StreamReader reader = new StreamReader(await (await gitHubClient.HttpClient.GetAsync($"nforgeio/neonKUBE/{branch}/Charts/tree.txt")).Content.ReadAsStreamAsync()))
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
                                    var name = line.Trim();

                                    await entryStream.WriteAsync(await gitHubClient.HttpClient.GetByteArrayAsync($"nforgeio/neonKUBE/{branch}/Charts/{name}"));
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
