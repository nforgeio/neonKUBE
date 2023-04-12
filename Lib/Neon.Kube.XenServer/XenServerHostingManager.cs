//-----------------------------------------------------------------------------
// FILE:	    XenServerHostingManager.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s.Models;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Kube.ClusterDef;
using Neon.Kube.Login;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;
using Neon.Net;
using Neon.XenServer;
using Neon.IO;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Hosting.XenServer
{
    /// <summary>
    /// Manages cluster provisioning on the XenServer hypervisor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional capability support:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="HostingCapabilities.Pausable"/></term>
    ///     <description><b>YES</b></description>
    /// </item>
    /// <item>
    ///     <term><see cref="HostingCapabilities.Stoppable"/></term>
    ///     <description><b>YES</b></description>
    /// </item>
    /// </list>
    /// </remarks>
    [HostingProvider(HostingEnvironment.XenServer)]
    public partial class XenServerHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Used to persist information about downloaded XVA template files.
        /// </summary>
        public class DiskTemplateInfo
        {
            /// <summary>
            /// The downloaded file ETAG.
            /// </summary>
            [JsonProperty(PropertyName = "ETag", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [YamlMember(Alias = "etag", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public string ETag { get; set; }

            /// <summary>
            /// The downloaded file length used as a quick verification that
            /// the complete file was downloaded.
            /// </summary>
            [JsonProperty(PropertyName = "Length", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [YamlMember(Alias = "length", ApplyNamingConventions = false)]
            [DefaultValue(-1)]
            public long Length { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to limit how many threads will be created by parallel operations.
        /// </summary>
        private static readonly ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = MaxAsyncParallelHostingOperations };

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this method.
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterProxy                cluster;
        private string                      nodeImageUri;
        private string                      nodeImagePath;
        private SetupController<XenClient>  xenController;
        private string                      driveTemplatePath;
        private string                      logFolder;
        private List<XenClient>             xenClients;
        private int                         maxVmNameWidth;
        private string                      secureSshPassword;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public XenServerHostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of provisioning a cluster on XenServer/XCP-ng servers.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="cloudMarketplace">Ignored.</param>
        /// <param name="nodeImageUri">Optionally specifies the node image URI.</param>
        /// <param name="nodeImagePath">Optionally specifies the path to the local node image file.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <remarks>
        /// <note>
        /// One of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be specified.
        /// </note>
        /// </remarks>
        public XenServerHostingManager(ClusterProxy cluster, bool cloudMarketplace, string nodeImageUri = null, string nodeImagePath = null, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            this.cluster                = cluster;
            this.nodeImageUri           = nodeImageUri;
            this.nodeImagePath          = nodeImagePath;
            this.cluster.HostingManager = this;
            this.logFolder              = logFolder;
            this.maxVmNameWidth         = cluster.SetupState.ClusterDefinition.Nodes.Max(node => node.Name.Length) + cluster.SetupState.ClusterDefinition.Hosting.Vm.GetVmNamePrefix(cluster.SetupState.ClusterDefinition).Length;

            // Create the [XenClient] instances that we'll use to manage the XenServer hosts.

            xenClients = new List<XenClient>();

            foreach (var host in cluster.SetupState.ClusterDefinition.Hosting.Vm.Hosts)
            {
                var hostAddress  = GetHostIpAddress(host);
                var hostname     = host.Name;
                var hostUsername = host.Username ?? cluster.SetupState.ClusterDefinition.Hosting.Vm.HostUsername;
                var hostPassword = host.Password ?? cluster.SetupState.ClusterDefinition.Hosting.Vm.HostPassword;

                if (string.IsNullOrEmpty(hostname))
                {
                    hostname = host.Address;
                }

                var xenClient = new XenClient(hostAddress, hostUsername, hostPassword, name: host.Name, logFolder: logFolder);

                xenClients.Add(xenClient);
            }
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (xenClients != null)
                {
                    foreach (var xenClient in xenClients)
                    {
                        xenClient.Dispose();
                    }

                    xenClients = null;
                }

                GC.SuppressFinalize(this);
            }

            xenClients = null;
        }

        /// <inheritdoc/>
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.XenServer;

        /// <inheritdoc/>
        public override bool RequiresNodeAddressCheck => true;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (clusterDefinition.Hosting.Environment != HostingEnvironment.XenServer)
            {
                throw new ClusterDefinitionException($"{nameof(HostingOptions)}.{nameof(HostingOptions.Environment)}] must be set to [{HostingEnvironment.XenServer}].");
            }

            if (clusterDefinition.Hosting == null || clusterDefinition.Hosting.Vm == null)
            {
                throw new ClusterDefinitionException($"{nameof(HostingOptions)}.{nameof(HostingOptions.Vm)}] property is required for XenServer clusters.");
            }

            var defaultHostUsername = clusterDefinition.Hosting.Vm.HostUsername;
            var defaultHostPassword = clusterDefinition.Hosting.Vm.HostPassword;

            if (clusterDefinition.Hosting.Vm.Hosts == null || clusterDefinition.Hosting.Vm.Hosts.Count == 0)
            {
                throw new ClusterDefinitionException($"{nameof(HostingOptions)}.{nameof(HostingOptions.Vm)}.{nameof(VmHostingOptions.Hosts)}] must specify at least one XenServer host.");
            }

            var hostSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var host in clusterDefinition.Hosting.Vm.Hosts)
            {
                if (string.IsNullOrEmpty(host.Username) && string.IsNullOrEmpty(defaultHostUsername))
                {
                    throw new ClusterDefinitionException($"XenServer host [{host.Name}] does not specify a [{nameof(host.Username)}] and there isn't a default username either.");
                }

                if (string.IsNullOrEmpty(host.Password) && string.IsNullOrEmpty(defaultHostPassword))
                {
                    throw new ClusterDefinitionException($"XenServer host [{host.Name}] does not specify a [{nameof(host.Password)}] and; there isn't a default password either.");
                }

                if (!hostSet.Contains(host.Name))
                {
                    hostSet.Add(host.Name);
                }
            }

            foreach (var node in clusterDefinition.Nodes)
            {
                if (node.Vm == null || string.IsNullOrEmpty(node.Vm.Host))
                {
                    throw new ClusterDefinitionException($"Cluster node [{node.Name}] does not specify a [{nameof(VmNodeOptions)}.{nameof(VmNodeOptions.Host)}]");
                }

                if (!hostSet.Contains(node.Vm.Host))
                {
                    throw new ClusterDefinitionException($"Cluster node [{node.Name}] references [host={node.Vm.Host}] which is not defined.");
                }
            }
        }

        /// <inheritdoc/>
        public override void AddProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Assert(cluster != null, $"[{nameof(XenServerHostingManager)}] was created with the wrong constructor.");

            // We need to ensure that the cluster has at least one ingress node.

            KubeHelper.EnsureIngressNodes(cluster.SetupState.ClusterDefinition);

            // Update the node labels with the actual capabilities of the 
            // virtual machines being provisioned.

            foreach (var node in cluster.SetupState.ClusterDefinition.Nodes)
            {
                node.Labels.PhysicalMachine = node.Vm.Host;
                node.Labels.ComputeCores    = node.Vm.GetCores(cluster.SetupState.ClusterDefinition);
                node.Labels.ComputeRam      = (int)(node.Vm.GetMemory(cluster.SetupState.ClusterDefinition) / ByteUnits.MebiBytes);
                node.Labels.StorageSize     = ByteUnits.ToGiB(node.Vm.GetOsDisk(cluster.SetupState.ClusterDefinition));
            }

            // Create [NodeSshProxy] instances that use the [XenClient] instances as proxy metadata.
            // Note that we're doing this to take advantage of [SetupController] to manage parallel
            // operations as well as to take advantage of existing UX progress code, but we're
            // never going to connect XenServers via [NodeSshProxy] and will use [XenClient]
            // to execute remote commands either via a local [xe-cli] or via the XenServer API
            // (in the future).
            //
            // NOTE: We're also going to add these proxies to the [ClusterProxy.Hosts] list so 
            //       that host proxy status updates will be included in the status event changes 
            //       raised to any UX.

            var xenSshProxies = new List<NodeSshProxy<XenClient>>();

            foreach (var host in cluster.SetupState.ClusterDefinition.Hosting.Vm.Hosts)
            {
                var hostAddress  = NetHelper.ParseIPv4Address(GetHostIpAddress(host));
                var hostname     = host.Name;
                var hostUsername = host.Username ?? cluster.SetupState.ClusterDefinition.Hosting.Vm.HostUsername;
                var hostPassword = host.Password ?? cluster.SetupState.ClusterDefinition.Hosting.Vm.HostPassword;

                if (string.IsNullOrEmpty(hostname))
                {
                    hostname = host.Address;
                }

                // $hack(jefflill):
                //
                // We need the [xenClient] and [sshProxy] to share the same log writer so that both
                // XenServer commands and normal step/status related logging will be written to
                // the log file for each xenClient.
                //
                // We're going to create the xenClient above (in the constructor) and then pass its
                // log writer to the proxy constructor.
                //
                // WARNING: This assumes that we'll never attempt to write to any given log on
                //          separate tasks or threads, which I believe is the case due to
                //          [SetupController] semantics.

                var xenClient = GetXenClient(hostname);
                var sshProxy  = new NodeSshProxy<XenClient>(hostname, hostAddress, SshCredentials.FromUserPassword(hostUsername, hostPassword), NodeRole.XenServer, logWriter: xenClient.LogWriter);

                sshProxy.Metadata = xenClient;

                xenSshProxies.Add(sshProxy);
                cluster.Hosts.Add(sshProxy);
            }

            // Associate the XenHosts with the setup controller so it will be able to include
            // information about faulted hosts in its global cluster log.

            controller.SetHosts(xenSshProxies.Select(host => (INodeSshProxy)host));

            // We're going to provision the XenServer hosts in parallel to
            // speed up cluster setup.  This works because each XenServer
            // host is essentially independent from the others.

            xenController = new SetupController<XenClient>($"Provisioning [{cluster.SetupState.ClusterDefinition.Name}] cluster", xenSshProxies, KubeHelper.LogFolder)
            {
                MaxParallel = this.MaxParallel
            };

            xenController.AddGlobalStep("check xenserver",
                controller =>
                {
                    this.secureSshPassword = cluster.SetupState.SshPassword;
                });

            xenController.AddWaitUntilOnlineStep();

            if (!controller.Get<bool>(KubeSetupProperty.DisableImageDownload, false))
            {
                xenController.AddNodeStep("xenserver node image", (controller, hostProxy) => InstallVmTemplateAsync(hostProxy), parallelLimit: 1);
            }

            var createVmLabel = "create virtual machine";

            if (cluster.SetupState.ClusterDefinition.Nodes.Count() > 1)
            {
                createVmLabel += "(s)";
            }

            xenController.AddNodeStep(createVmLabel, (controller, hostProxy) => ProvisionVM(hostProxy));
            xenController.AddNodeStep("clear host status", (controller, hostProxy) => hostProxy.Status = string.Empty, quiet: true);

            controller.AddControllerStep(xenController);
        }

        /// <inheritdoc/>
        public override void AddPostProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            if (cluster.SetupState.ClusterDefinition.Storage.OpenEbs.Engine == OpenEbsEngine.cStor)
            {
                // We need to add any required OpenEBS cStor disks after the node has been otherwise
                // prepared.  We need to do this here because if we created the data and OpenEBS disks
                // when the VM is initially created, the disk setup scripts executed during prepare
                // won't be able to distinguish between the two disks.
                //
                // At this point, the data disk should be partitioned, formatted, and mounted so
                // the OpenEBS disk will be easy to identify as the only unpartitioned disk.

                // IMPLEMENTATION NOTE:
                // --------------------
                // This is a bit tricky.  The essential problem is that the setup controller passed
                // is intended for parallel operations on nodes, not XenServer hosts (like we did
                // above for provisioning).  We still have those XenServer host clients in the [xenClients]
                // list field.  Note that XenClients are not thread-safe.
                // 
                // We're going to perform these operations in parallel, but require that each node
                // operation acquire a lock on the XenClient for the node's host before proceeding.

                controller.AddNodeStep("openebs",
                    (controller, node) =>
                    {
                        var xenClient = xenClients.Single(client => client.Name == node.Metadata.Vm.Host);

                        node.Status = "openebs: waiting for host...";

                        lock (xenClient)
                        {
                            var vm = xenClient.Machine.List().Single(vm => vm.NameLabel == GetVmName(node));

                            if (xenClient.Machine.DiskCount(vm) < 2)
                            {
                                // We haven't created the cStor disk yet.

                                var disk = new XenVirtualDisk()
                                {
                                    Name        = $"{GetVmName(node)}: openebs",
                                    Size        = node.Metadata.Vm.GetOpenEbsDiskSizeBytes(cluster.SetupState.ClusterDefinition),
                                    Description = "OpenEBS cStor"
                                };

                                node.Status = "openebs: stop VM";
                                xenClient.Machine.Shutdown(vm);

                                node.Status = "openebs: add cStor disk";
                                xenClient.Machine.AddDisk(vm, disk);

                                node.Status = "openebs: restart VM";
                                xenClient.Machine.Start(vm);

                                node.Status = string.Empty;
                            }
                        }
                    },
                    (controller, node) => node.Metadata.OpenEbsStorage);
            }
        }

        /// <summary>
        /// Returns the IP address for a XenServer host, performing a DNS lookup if necessary.
        /// </summary>
        /// <param name="host">The XenServer host information.</param>
        /// <returns>The IP address.</returns>
        /// <exception cref="NeonKubeException">Thrown if the address could not be obtained.</exception>
        private string GetHostIpAddress(HypervisorHost host)
        {
            if (!NetHelper.TryParseIPv4Address(host.Address, out var ipAddress))
            {
                try
                {
                    var addresses = Dns.GetHostAddresses(host.Address);

                    if (addresses.Length == 0)
                    {
                        throw new NeonKubeException($"DNS lookup failed for: {host.Address}");
                    }

                    ipAddress = addresses.First();
                }
                catch (Exception e)
                {
                    throw new NeonKubeException($"DNS lookup failed for: {host.Address}", e);
                }
            }

            return ipAddress.ToString();
        }

        /// <summary>
        /// Returns the list of <see cref="NodeDefinition"/> instances describing which cluster
        /// nodes are to be hosted by a specific XenServer.
        /// </summary>
        /// <param name="xenClient">The target XenServer.</param>
        /// <returns>The list of nodes to be hosted on the XenServer.</returns>
        private List<NodeSshProxy<NodeDefinition>> GetHostedNodes(XenClient xenClient)
        {
            var nodeDefinitions = cluster.SetupState.ClusterDefinition.NodeDefinitions.Values;

            return cluster.Nodes.Where(node => node.Metadata.Vm.Host.Equals(xenClient.Name, StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(node => node.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Fetches the <see cref="XenClient"/> by name.
        /// </summary>
        /// <param name="hostname">The XenServer host name.</param>
        /// <returns>The <see cref="XenClient"/>.</returns>
        private XenClient GetXenClient(string hostname)
        {
            return xenClients.Single(xenClient => xenClient.Name.Equals(hostname, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Returns the name to use when naming the virtual machine hosting the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeSshProxy<NodeDefinition> node)
        {
            return $"{cluster.SetupState.ClusterDefinition.Hosting.Vm.GetVmNamePrefix(cluster.SetupState.ClusterDefinition)}{node.Name}";
        }

        /// <summary>
        /// Returns the name to use for the virtual machine that will host the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            return $"{cluster.SetupState.ClusterDefinition.Hosting.Vm.GetVmNamePrefix(cluster.SetupState.ClusterDefinition)}{node.Name}";
        }

        /// <summary>
        /// Converts a virtual machine name to the matching node definition.
        /// </summary>
        /// <param name="vmName">The virtual machine name.</param>
        /// <returns>The matching node definition or <c>null</c>.</returns>
        private NodeDefinition VmNameToNodeDefinition(string vmName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(vmName), nameof(vmName));

            var prefix = cluster.SetupState.ClusterDefinition.Hosting.Vm.GetVmNamePrefix(cluster.SetupState.ClusterDefinition);

            if (!vmName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var nodeName = vmName.Substring(prefix.Length);

            if (cluster.SetupState.ClusterDefinition.NodeDefinitions.TryGetValue(nodeName, out var nodeDefinition))
            {
                return nodeDefinition;
            }

            return null;
        }

        /// <summary>
        /// Install the virtual machine template on the XenServer if it's not already present.
        /// </summary>
        /// <param name="xenSshProxy">The XenServer SSH proxy.</param>
        private async Task InstallVmTemplateAsync(NodeSshProxy<XenClient> xenSshProxy)
        {
            await SyncContext.Clear;

            var xenClient    = xenSshProxy.Metadata;
            var templateName = $"neonkube-{KubeVersions.NeonKubeWithBranchPart}";

            // Download the node template to the workstation if it's not already present.

            if (nodeImagePath != null)
            {
                Covenant.Assert(File.Exists(nodeImagePath));

                driveTemplatePath = nodeImagePath;
            }
            else
            {
                xenSshProxy.Status = $"download: node image [{templateName}]";

                xenController.SetGlobalStepStatus();

                string driveTemplateName;

                if (!string.IsNullOrEmpty(nodeImageUri))
                {
                    // Download the GZIPed XVA template if it's not already present and has a valid
                    // MD5 hash file.
                    //
                    // NOTE: We're going to name the file the same as the file name from the URI.

                    var driveTemplateUri = new Uri(nodeImageUri);

                    driveTemplateName = Path.GetFileNameWithoutExtension(driveTemplateUri.Segments.Last());
                    driveTemplatePath = Path.Combine(KubeHelper.VmImageFolder, driveTemplateName);

                    await KubeHelper.DownloadNodeImageAsync(nodeImageUri, driveTemplatePath,
                        (progressType, progress) =>
                        {
                            xenController.SetGlobalStepStatus($"{NeonHelper.EnumToString(progressType)}: XVA [{progress}%] [{driveTemplateName}]");

                            return !xenController.IsCancelPending;
                        });
                }
                else
                {
                    Covenant.Assert(File.Exists(nodeImagePath), $"Missing file: {nodeImagePath}");

                    driveTemplateName = Path.GetFileName(nodeImagePath);
                    driveTemplatePath = nodeImagePath;
                }
            }

            // The MD5 is computed for the GZIP compressed node image as downloaded
            // from the source.  We're expecting a file at $"{driveTemplatePath}.gz" 
            // holding this MD5 (this file is created during the multi-part download).
            // 
            // It's possible that the MD5 file won't exist for maintainer who are using
            // a cached template file.  We'll recompute the MD5 here in that case and
            // save the file for next time.

            string templateMd5;
            string templateMd5Path = $"{driveTemplatePath}.md5";

            if (File.Exists(templateMd5Path))
            {
                templateMd5 = File.ReadAllText(templateMd5Path).Trim();
            }
            else
            {
                xenSshProxy.Status = $"compute: node image MD5 [{templateMd5Path}]";
                xenController.SetGlobalStepStatus();

                using (var templateStream = File.OpenRead(driveTemplatePath))
                {
                    templateMd5 = CryptoHelper.ComputeMD5String(templateStream);
                }

                File.WriteAllText(templateMd5Path, templateMd5);
            }

            // We need to check for an existing node template on the XenServer and
            // compare the MD5 encoded in the remote template description against
            // the local template MD5.  The remote description should look like:
            //
            //      neonKUBE Node Image [md5:ff97e7c555e32442ea8e8c7cb12d14df]
            //
            // We'll return immediately if the description MD5 matches otherwise
            // we'll remove the remote template and import a new one.

            xenSshProxy.Status = $"check: node image [{templateName}]";
            xenController.SetGlobalStepStatus();

            var template = xenClient.Template.Find(templateName);

            if (template != null)
            {
                var description = template.NameDescription;
                var md5RegEx    = new Regex(@"\[MD5:(?<md5>[0-9a-f]{32})\]");
                var match       = md5RegEx.Match(description);

                if (match.Success)
                {
                    // The description has an encoded MD5 so compare that against the
                    // MD5 for the local template.  We're done when they match.

                    if (templateMd5 == match.Groups["md5"].Value)
                    {
                        xenSshProxy.Status = $"check: node image [{templateName}] MD5 match";
                        xenController.SetGlobalStepStatus();
                        return;
                    }
                }

                // We'll arrive here if the existing template's description wasn't
                // formatted correctly or if the MD5 didn't match the local template.
                // 
                // We'll remove the existing template and then import a new one 
                // below.
                //
                // NOTE: Template removal will fail when any VMs on the XenServer
                //       reference the template in snapshhot mode.  This won't be an issue
                //       for end-users because published templates will be invariant,
                //       but maintainers will need to manually remove the VMs and
                //       try again.
                //
                //       This also won't be an issue for VMs that weren't created
                //       with snapshot mode.

                xenSshProxy.Status = $"check: Node image MD5 mismatch; deleting [{templateName}]";
                xenController.SetGlobalStepStatus();

                try
                {
                    xenClient.Template.Destroy(template);
                }
                catch (XenException)
                {
                    xenSshProxy.LogLine($"Cannot delete [{templateName}].  You may need to delete VMs referencing this template using snapshot mode.");
                    throw;
                }
            }

            // Install the node template on the XenServer.  Note that we're going to
            // add a description formatted like:
            //
            //      neonKUBE Node Image [MD5:ff97e7c555e32442ea8e8c7cb12d14df]
            //
            // The MD5 is computed for the GZIP compressed node image as downloaded
            // from the source.  We're expecting a file at $"{driveTemplatePath}.gz" 
            // holding this MD5 (this file is when the multi-part template was downloaded.
            // 
            // It's possible that the MD5 file won't exist for maintainers who are using
            // a cached template file.  In this case, we'll recompute the MD5 here.

            string md5;
            string md5Path = $"{driveTemplatePath}.md5";

            if (File.Exists(md5Path))
            {
                md5 = File.ReadAllText(md5Path).Trim();
            }
            else
            {
                xenSshProxy.Status = $"compute: node image MD5: {templateName}";
                xenController.SetGlobalStepStatus();

                using (var templateStream = File.OpenRead(driveTemplatePath))
                {
                    md5 = CryptoHelper.ComputeMD5String(templateStream);
                }
            }

            xenSshProxy.Status = $"install: node image {templateName} (slow)";
            xenController.SetGlobalStepStatus();

            xenClient.Template.ImportVmTemplate(driveTemplatePath, templateName, cluster.SetupState.ClusterDefinition.Hosting.XenServer.StorageRepository, description: $"neonKUBE Node Image [MD5:{md5}]");

            xenSshProxy.Status = string.Empty;
            xenController.SetGlobalStepStatus();
        }

        /// <summary>
        /// Formats a nice node status message.
        /// </summary>
        /// <param name="vmName">The name of the virtual machine used to host the cluster node.</param>
        /// <param name="message">The status message.</param>
        /// <returns>The formatted status message.</returns>
        private string FormatVmStatus(string vmName, string message)
        {
            var namePart     = $"[{vmName}]:";
            var desiredWidth = maxVmNameWidth + 3;
            var actualWidth  = namePart.Length;

            if (desiredWidth > actualWidth)
            {
                namePart += new string(' ', desiredWidth - actualWidth);
            }

            return $"{namePart} {message}";
        }

        /// <summary>
        /// Provision the virtual machines on the XenServer.
        /// </summary>
        /// <param name="xenSshProxy">The XenServer SSH proxy.</param>
        private void ProvisionVM(NodeSshProxy<XenClient> xenSshProxy)
        {
            var xenClient = xenSshProxy.Metadata;
            var hostInfo  = xenClient.GetHostInfo();

            if (hostInfo.Version < KubeVersions.MinXenServerVersion)
            {
                throw new NotSupportedException($"neonKUBE cannot provision a cluster on a XenServer/XCP-ng host older than [v{KubeVersions.MinXenServerVersion}].  [{hostInfo.Params["name-label"]}] is running version [{hostInfo.Version}]. ");
            }

            foreach (var node in GetHostedNodes(xenClient))
            {
                var vmName      = GetVmName(node);
                var cores       = node.Metadata.Vm.GetCores(cluster.SetupState.ClusterDefinition);
                var memoryBytes = node.Metadata.Vm.GetMemory(cluster.SetupState.ClusterDefinition);
                var osDiskBytes = node.Metadata.Vm.GetOsDisk(cluster.SetupState.ClusterDefinition);

                xenSshProxy.Status = FormatVmStatus(vmName, "create: virtual machine");

                var vm = xenClient.Machine.Create(vmName, $"neonkube-{KubeVersions.NeonKubeWithBranchPart}",
                    cores:                      cores,
                    memoryBytes:                memoryBytes,
                    diskBytes:                  osDiskBytes,
                    snapshot:                   cluster.SetupState.ClusterDefinition.Hosting.XenServer.Snapshot,
                    primaryStorageRepository:   cluster.SetupState.ClusterDefinition.Hosting.XenServer.StorageRepository);

                xenSshProxy.Status = string.Empty;

                // Create a temporary ISO with the [neon-init.sh] script, mount it
                // to the VM and then boot the VM for the first time.  The script on the
                // ISO will be executed automatically by the [neon-init] service
                // preinstalled on the VM image and the script will configure the secure 
                // SSH password and then the network.
                //
                // This ensures that SSH is not exposed to the network before the secure
                // password has been set.

                var tempIso    = (TempFile)null;
                var xenTempIso = (XenTempIso)null;

                try
                {
                    // Create a temporary ISO with the prep script and insert it
                    // into the node VM.

                    node.Status = $"mount: neon-init iso";

                    tempIso    = KubeHelper.CreateNeonInitIso(node.Cluster.SetupState.ClusterDefinition, node.Metadata, nodeMtu: NodeMtu, newPassword: secureSshPassword);
                    xenTempIso = xenClient.CreateTempIso(tempIso.Path);

                    xenClient.Invoke($"vm-cd-eject", $"uuid={vm.Uuid}");
                    xenClient.Invoke($"vm-cd-insert", $"uuid={vm.Uuid}", $"cd-name={xenTempIso.IsoName}");

                    // Start the VM for the first time with the mounted ISO.  The network
                    // configuration will happen automatically by the time we can connect.

                    node.Status = $"start: virtual machine";
                    xenClient.Machine.Start(vm);

                    // Update the node credentials to use the secure password and then wait for the node to boot.

                    node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, secureSshPassword));
                    node.WaitForBoot();

                    // Extend the primary partition and file system to fill 
                    // the virtual disk.
                    //
                    // Note that there should only be one unpartitioned disk
                    // at this point: the OS disk.

                    var partitionedDisks = node.ListPartitionedDisks();
                    var osDisk           = partitionedDisks.Single();

                    node.Status = $"resize: OS disk";

                    var response = node.SudoCommand($"growpart {osDisk} 2", RunOptions.None);

                    // Ignore errors reported when the partition is already at its
                    // maximum size and cannot be grown:
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/1352

                    if (!response.Success && !response.AllText.Contains("NOCHANGE:"))
                    {
                        response.EnsureSuccess();
                    }

                    node.SudoCommand($"resize2fs {osDisk}2", RunOptions.FaultOnError);
                }
                finally
                {
                    // Be sure to delete the local and remote ISO files so these don't accumulate.

                    tempIso?.Dispose();

                    // These can also accumulate on the XenServer.

                    if (xenTempIso != null)
                    {
                        xenClient.Invoke($"vm-cd-eject", $"uuid={vm.Uuid}");
                        xenClient.RemoveTempIso(xenTempIso);
                    }

                    node.Status        = string.Empty;
                    xenSshProxy.Status = string.Empty;
                }
            }
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override string GetDataDisk(LinuxSshProxy node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            // This hosting manager doesn't currently provision a separate data disk.

            return "/dev/xvdb1"; //"PRIMARY";
        }

        /// <inheritdoc/>
        public override IEnumerable<string> GetClusterAddresses()
        {
            if (cluster.SetupState.PublicAddresses?.Any() ?? false)
            {
                return cluster.SetupState.PublicAddresses;
            }

            return cluster.SetupState.ClusterDefinition.ControlNodes.Select(controlPlane => controlPlane.Address);
        }

        /// <inheritdoc/>
        public override async Task<HostingResourceAvailability> GetResourceAvailabilityAsync(long reserveMemory = 0, long reserveDisk = 0)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentException>(reserveMemory >= 0, nameof(reserveMemory));
            Covenant.Requires<ArgumentException>(reserveDisk >= 0, nameof(reserveDisk));

            // NOTE: We're going to allow CPUs to be oversubscribed but not RAM or disk.
            //       We will honor the memory and disk reservations for XenServer.

            var availability = new HostingResourceAvailability();

            //-----------------------------------------------------------------
            // Create a dictionary mapping the XenServer host name to a record
            // holding a reference to the associated XenClient as well as information
            // about the host resources.
            //
            // We'll set the capacity for a host to NULL when capacity fetch calls
            // fail and report and immediately report host offline constraints.

            var hostnameToCapacity = new Dictionary<string, XenHostInfo>(StringComparer.InvariantCultureIgnoreCase);

            Parallel.ForEach(xenClients, parallelOptions,
                xenClient =>
                {
                    try
                    {
                        var hostInfo = xenClient.GetHostInfo();

                        lock (hostnameToCapacity)
                        {
                            hostnameToCapacity.Add(xenClient.Name, hostInfo);
                        }
                    }
                    catch (XenException)
                    {
                        // We're going to consider the host to be offline.

                        lock (hostnameToCapacity)
                        {
                            hostnameToCapacity.Add(xenClient.Name, null);
                        }
                    }
                });

            if (hostnameToCapacity.Values.Any(capacity => capacity == null))
            {
                availability.Constraints = new Dictionary<string, List<HostingResourceConstraint>>();

                foreach (var offlineHostname in hostnameToCapacity
                    .Where(item => item.Value == null)
                    .Select(item => item.Key)
                    .OrderBy(key => key, StringComparer.InvariantCultureIgnoreCase))
                {
                    if (!availability.Constraints.TryGetValue(offlineHostname, out var hostConstraints))
                    {
                        hostConstraints = new List<HostingResourceConstraint>();

                        availability.Constraints.Add(offlineHostname, hostConstraints);
                    }

                    var constraint = 
                        new HostingResourceConstraint()
                        {
                            ResourceType = HostingConstrainedResourceType.VmHost,
                            Details      = "XenServer host is offline",
                            Nodes        = cluster.SetupState.ClusterDefinition.Nodes
                                               .Where(node => node.Vm.Host.Equals(offlineHostname, StringComparison.InvariantCultureIgnoreCase))
                                               .OrderBy(node => node.Name)
                                               .Select(node => node.Name)
                                               .ToList()
                        };

                    hostConstraints.Add(constraint); 
                }

                return availability;
            }

            // Total the memory and disk space required for the cluster nodes on each XenServer host.

            var hostnameToRequiredMemory = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);
            var hostnameToRequiredDisk   = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var xenClient in xenClients)
            {
                hostnameToRequiredMemory.Add(xenClient.Name, 0);
                hostnameToRequiredDisk.Add(xenClient.Name, 0);
            }

            foreach (var node in cluster.SetupState.ClusterDefinition.Nodes)
            {
                var hostname = node.Vm.Host;

                hostnameToRequiredMemory[hostname] += node.Vm.GetMemory(cluster.SetupState.ClusterDefinition);

                var requiredDiskForNode = node.Vm.GetOsDisk(cluster.SetupState.ClusterDefinition);

                if (node.OpenEbsStorage)
                {
                    switch (cluster.SetupState.ClusterDefinition.Storage.OpenEbs.Engine)
                    {
                        case OpenEbsEngine.cStor:
                        case OpenEbsEngine.Mayastor:

                            requiredDiskForNode += node.Vm.GetOpenEbsDiskSizeBytes(cluster.SetupState.ClusterDefinition);
                            break;

                        default:

                            break;  // The other engines don't provision an extra drive.
                    }
                }

                hostnameToRequiredDisk[hostname] += requiredDiskForNode;
            }

            //-----------------------------------------------------------------
            // Construct and return the resource availability.

            var hostNodes = cluster.SetupState.ClusterDefinition.Nodes.ToLookup(node => node.Vm.Host, node => node);

            foreach (var hostNodeGroup in hostNodes)
            {
                var hostname = hostNodeGroup.Key;

                // Ensure that each host has sufficient resources to accommodate the nodes
                // assigned to it and update the availability constraints when this isn't
                // the case.

                var hostCapacity = hostnameToCapacity[hostname];

                // Check memory.

                var requiredMemory  = hostnameToRequiredMemory[hostNodeGroup.Key];
                var availableMemory = hostCapacity.AvailableMemory;

                if (requiredMemory > availableMemory)
                {
                    if (!availability.Constraints.TryGetValue(hostname, out var constraints))
                    {
                        constraints = new List<HostingResourceConstraint>();

                        availability.Constraints.Add(hostname, constraints);
                    }

                    var humanRequiredMemory  = ByteUnits.Humanize(requiredMemory, powerOfTwo: true);
                    var humanAvailableMemory = ByteUnits.Humanize(availableMemory, powerOfTwo: true);

                    constraints.Add(
                        new HostingResourceConstraint()
                        {
                             ResourceType = HostingConstrainedResourceType.Memory,
                             Nodes        = hostNodeGroup.Select(node => node.Name).ToList(),
                             Details      = $"[{humanRequiredMemory}] physical memory is required but only [{humanAvailableMemory}] is available."
                        });
                }

                // Check disk.

                var requiredDisk  = hostnameToRequiredDisk[hostNodeGroup.Key];
                var availableDisk = hostCapacity.AvailableDisk;

                if (requiredDisk > availableDisk)
                {
                    if (!availability.Constraints.TryGetValue(hostname, out var constraints))
                    {
                        constraints = new List<HostingResourceConstraint>();

                        availability.Constraints.Add(hostname, constraints);
                    }

                    var humanRequiredDisk  = ByteUnits.Humanize(requiredDisk, powerOfTwo: true);
                    var humanAvailableDisk = ByteUnits.Humanize(availableDisk, powerOfTwo: true);

                    constraints.Add(
                        new HostingResourceConstraint()
                        {
                             ResourceType = HostingConstrainedResourceType.Disk,
                             Nodes        = hostNodeGroup.Select(node => node.Name).ToList(),
                             Details      = $"[{humanRequiredDisk}] disk is required but only [{humanAvailableDisk}] is available."
                        });
                }
            }

            return availability;
        }

        //---------------------------------------------------------------------
        // Cluster life-cycle methods

        // $todo(jefflill):
        //
        // XenServer is having trouble suspending VMs so I'm going to disable this
        // feature for the time being:
        //
        //      https://github.com/nforgeio/neonKUBE/issues/1488

        /// <inheritdoc/>
        public override HostingCapabilities Capabilities => HostingCapabilities.Stoppable /* | HostingCapabilities.Pausable */ | HostingCapabilities.Removable;

        /// <inheritdoc/>
        public override async Task<ClusterHealth> GetClusterHealthAsync(TimeSpan timeout = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(XenServerHostingManager)}] was created with the wrong constructor.");

            var clusterHealth = new ClusterHealth();

            if (timeout <= TimeSpan.Zero)
            {
                timeout = DefaultStatusTimeout;
            }

            // Create a dictionary mapping XenServer host names to their clients and
            // then another dictionary that maps XenServer host names to a dictionary
            // of virtual machines on that host keyed by machine name.
            //
            // Hosts that don't reply will be considered to be ofline and will have
            // their corresponding host name dictionaries set to NULL.  Note that
            // we're going query the hosts in parallel.

            var hostnameToXenClient = xenClients.ToDictionary(client => client.Name, client => client, StringComparer.InvariantCultureIgnoreCase);
            var hostnameToVms       = new Dictionary<string, Dictionary<string, XenVirtualMachine>>(StringComparer.InvariantCultureIgnoreCase);

            Parallel.ForEach(hostnameToXenClient.Values, parallelOptions,
                hostClient =>
                {
                    try
                    {
                        var vmNameToMachine = new Dictionary<string, XenVirtualMachine>(StringComparer.InvariantCultureIgnoreCase);

                        foreach (var virtualMachine in hostClient.Machine.List())
                        {
                            vmNameToMachine[virtualMachine.NameLabel] = virtualMachine;
                        }

                        lock (hostnameToVms)
                        {
                            hostnameToVms[hostClient.Name] = vmNameToMachine;
                        }
                    }
                    catch (XenException)
                    {
                        // We're considering the XenServer host to be offline.

                        lock (hostnameToVms)
                        {
                            hostnameToVms[hostClient.Name] = null;
                        }
                    }
                });

            // We're going to infer the cluster provisiong status by examining the
            // cluster login and the state of the VMs deployed to the XenServer hosts.

            var contextName = $"root@{cluster.SetupState.ClusterDefinition.Name}";
            var context     = KubeHelper.Config.GetContext(contextName);

            // Create a hashset holding the names of nodes that have existing virtual machines
            // and also construct a dictionary mapping the actual virtual machine names to
            // the machine information.

            var existingNodes    = new HashSet<string>();
            var existingMachines = new Dictionary<string, XenVirtualMachine>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var nodeNameToVm in hostnameToVms.Values)
            {
                if (nodeNameToVm == null)
                {
                    // Host is offline.

                    continue;
                }

                foreach (var machine in nodeNameToVm.Values)
                {
                    var nodeDefinition = VmNameToNodeDefinition(machine.NameLabel);

                    if (nodeDefinition != null)
                    {
                        existingNodes.Add(nodeDefinition.Name);
                    }

                    existingMachines.Add(machine.NameLabel, machine);
                }
            }

            // Report when any XenServer hosts are offline.

            var offlineHostnames = hostnameToVms
                .Where(item => item.Value == null)
                .Select(item => item.Key)
                .ToArray();

            if (offlineHostnames.Length > 0)
            {
                var sbOfflineHostnames = new StringBuilder();

                foreach (var hostname in offlineHostnames.OrderBy(hostname => hostname, StringComparer.InvariantCultureIgnoreCase))
                {
                    sbOfflineHostnames.AppendWithSeparator(hostname);
                }

                clusterHealth.State   = ClusterState.Unhealthy;
                clusterHealth.Summary = $"XenServer hosts are offline: {sbOfflineHostnames}";

                return clusterHealth;
            }

            // Build the cluster status.

            if (context == null)
            {
                // The Kubernetes context for this cluster doesn't exist, so we know that any
                // virtual machines with names matching the virtual machines that would be
                // provisioned for the cluster definition are conflicting.

                clusterHealth.State   = ClusterState.NotFound;
                clusterHealth.Summary = "Cluster does not exist";

                foreach (var node in cluster.SetupState.ClusterDefinition.NodeDefinitions.Values)
                {
                    clusterHealth.Nodes.Add(node.Name, existingNodes.Contains(node.Name) ? ClusterNodeState.Conflict : ClusterNodeState.NotProvisioned);
                }

                return clusterHealth;
            }
            else
            {
                // We're going to assume that all virtual machines that match cluster node names
                // (after stripping off any cluster prefix) belong to the cluster and will
                // map the actual VM states to public node states.

                foreach (var node in cluster.SetupState.ClusterDefinition.NodeDefinitions.Values)
                {
                    var nodeState = ClusterNodeState.NotProvisioned;

                    if (existingNodes.Contains(node.Name))
                    {
                        var vmName = GetVmName(node);

                        if (existingMachines.TryGetValue(vmName, out var machine))
                        {
                            switch (machine.PowerState)
                            {
                                case XenVmPowerState.Unknown:

                                    nodeState = ClusterNodeState.Unknown;
                                    break;

                                case XenVmPowerState.Halted:

                                    nodeState = ClusterNodeState.Off;
                                    break;

                                case XenVmPowerState.Paused:

                                    nodeState = ClusterNodeState.Paused;
                                    break;

                                case XenVmPowerState.Running:

                                    nodeState = ClusterNodeState.Running;
                                    break;

                                default:

                                    throw new NotImplementedException();
                            }
                        }
                    }

                    clusterHealth.Nodes.Add(node.Name, nodeState);
                }

                // We're going to examine the node states from the XenServer perspective and
                // short-circuit the Kubernetes level cluster health check when the cluster
                // nodes are not provisioned, are paused or appear to be transitioning
                // between starting, stopping, or paused states.

                var commonNodeState = clusterHealth.Nodes.Values.First();

                foreach (var nodeState in clusterHealth.Nodes.Values)
                {
                    if (nodeState != commonNodeState)
                    {
                        // Nodes have differing states so we're going to consider the cluster
                        // to be transitioning.

                        clusterHealth.State   = ClusterState.Transitioning;
                        clusterHealth.Summary = "Cluster is transitioning";
                        break;
                    }
                }

                if (cluster.SetupState.DeploymentStatus != ClusterDeploymentStatus.Ready)
                {
                    clusterHealth.State   = ClusterState.Configuring;
                    clusterHealth.Summary = "Cluster is partially configured";
                }
                else if (clusterHealth.State != ClusterState.Transitioning)
                {
                    // If we get here then all of the nodes have the same state so
                    // we'll use that common state to set the overall cluster state.

                    switch (commonNodeState)
                    {
                        case ClusterNodeState.Paused:

                            clusterHealth.State   = ClusterState.Paused;
                            clusterHealth.Summary = "Cluster is paused";
                            break;

                        case ClusterNodeState.Starting:

                            clusterHealth.State   = ClusterState.Unhealthy;
                            clusterHealth.Summary = "Cluster is starting";
                            break;

                        case ClusterNodeState.Running:

                            clusterHealth.State   = ClusterState.Healthy;
                            clusterHealth.Summary = "Cluster is configured";
                            break;

                        case ClusterNodeState.Off:

                            clusterHealth.State   = ClusterState.Off;
                            clusterHealth.Summary = "Cluster is turned off";
                            break;

                        case ClusterNodeState.NotProvisioned:

                            clusterHealth.State   = ClusterState.NotFound;
                            clusterHealth.Summary = "Cluster is not found.";
                            break;

                        case ClusterNodeState.Unknown:
                        default:

                            clusterHealth.State   = ClusterState.NotFound;
                            clusterHealth.Summary = "Cluster not found";
                            break;
                    }
                }

                if (clusterHealth.State == ClusterState.Off)
                {
                    clusterHealth.Summary = "Cluster is turned off";

                    return clusterHealth;
                }

                return clusterHealth;
            }
        }

        /// <inheritdoc/>
        public override async Task StartClusterAsync()
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(XenServerHostingManager)}] was created with the wrong constructor.");

            // We just need to start any cluster VMs that aren't already running.

            Parallel.ForEach(cluster.SetupState.ClusterDefinition.Nodes, parallelOptions,
                node =>
                {
                    var vmName    = GetVmName(node);
                    var xenClient = xenClients.Single(xenClient => xenClient.Name.Equals(node.Vm.Host, StringComparison.InvariantCultureIgnoreCase));
                    var vm        = xenClient.Machine.Find(name: vmName);

                    if (vm == null)
                    {
                        // We may see this when the cluster definition doesn't match the 
                        // deployed cluster VMs.  We're just going to ignore this.

                        return;
                    }

                    switch (vm.PowerState)
                    {
                        case XenVmPowerState.Halted:
                        case XenVmPowerState.Paused:

                            xenClient.Machine.Start(vm);
                            break;

                        case XenVmPowerState.Running:

                            break;

                        default:
                        case XenVmPowerState.Unknown:

                            throw new NotImplementedException($"Unexpected VM state: {vmName}:{vm.PowerState}");
                    }
                });
        }

        /// <inheritdoc/>
        public override async Task StopClusterAsync(StopMode stopMode = StopMode.Graceful)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(XenServerHostingManager)}] was created with the wrong constructor.");

            // We just need to stop any running cluster VMs.

            Parallel.ForEach(cluster.SetupState.ClusterDefinition.Nodes,
                node =>
                {
                    var vmName    = GetVmName(node);
                    var xenClient = GetXenClient(node.Vm.Host);
                    var vm        = xenClient.Machine.Find(vmName);

                    if (vm == null)
                    {
                        // We may see this when the cluster definition doesn't match the 
                        // deployed cluster VMs.  We're just going to ignore this situation.

                        return;
                    }

                    switch (vm.PowerState)
                    {
                        case XenVmPowerState.Halted:

                            break;

                        case XenVmPowerState.Paused:

                            throw new NotSupportedException($"Cannot shutdown the saved (hibernating) virtual machine: {vmName}");

                        case XenVmPowerState.Running: 

                            switch (stopMode)
                            {
                                case StopMode.Pause:

                                    xenClient.Machine.Suspend(vm);
                                    break;

                                case StopMode.Graceful:

                                    xenClient.Machine.Shutdown(vm);
                                    break;

                                case StopMode.TurnOff:

                                    xenClient.Machine.Shutdown(vm, turnOff: true);
                                    break;
                            }
                            break;

                        default:
                        case XenVmPowerState.Unknown:

                            throw new NotImplementedException($"Unexpected VM power state: {vm.NameLabel}:{vm.PowerState}");
                    }
                });
        }

        /// <inheritdoc/>
        public override async Task DeleteClusterAsync(bool removeOrphans = false)
        {
            await SyncContext.Clear;
            Covenant.Requires<NotSupportedException>(cluster != null, $"[{nameof(XenServerHostingManager)}] was created with the wrong constructor.");

            // If [removeOrphans=true] and the cluster definition specifies a
            // VM name prefix, then we'll simply remove all VMs with that prefix
            // that exist on any of the target XenServer hosts.
            //
            // Otherwise, we'll do a normal remove.

            var vmPrefix = cluster.SetupState.ClusterDefinition.Hosting.Vm.GetVmNamePrefix(cluster.SetupState.ClusterDefinition);

            if (removeOrphans && !string.IsNullOrEmpty(vmPrefix))
            {
                Parallel.ForEach(xenClients, parallelOptions,
                    xenClient =>
                    {
                        Parallel.ForEach(xenClient.Machine.List().Where(vm => vm.NameLabel.StartsWith(vmPrefix)), parallelOptions,
                            vm =>
                            {
                                xenClient.Machine.Remove(vm, keepDrives: false);
                            });
                    });

                return;
            }

            // All we need to do for Hyper-V clusters is turn off and remove the cluster VMs.
            // Note that we're just turning nodes off to save time and because we're going
            // to be deleting them all anyway.

            await StopClusterAsync(stopMode: StopMode.TurnOff);

            Parallel.ForEach(cluster.SetupState.ClusterDefinition.Nodes,
                node =>
                {
                    var vmName    = GetVmName(node);
                    var xenClient = GetXenClient(node.Vm.Host);
                    var vm        = xenClient.Machine.Find(vmName);

                    if (vm == null)
                    {
                        // We may see this when the cluster definition doesn't match the 
                        // deployed cluster VMs or if a VM has already been manually deleted.
                        //
                        // We're just going to ignore this situation.

                        return;
                    }

                    xenClient.Machine.Remove(vm, keepDrives: false);
                });
        }
    }
}
