//-----------------------------------------------------------------------------
// FILE:	    XenServerHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Net;
using Neon.Xen;

namespace Neon.Hive
{
    /// <summary>
    /// Manages hive provisioning on the XenServer hypervisor.
    /// </summary>
    [HostingProvider(HostingEnvironments.XenServer)]
    public partial class XenServerHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to persist information about downloaded VHD template files.
        /// </summary>
        public class DriveTemplateInfo
        {
            /// <summary>
            /// The downloaded file ETAG.
            /// </summary>
            [JsonProperty(PropertyName = "ETag", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(null)]
            public string ETag { get; set; }

            /// <summary>
            /// The downloaded file length used as a quick verification that
            /// the complete file was downloaded.
            /// </summary>
            [JsonProperty(PropertyName = "Length", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(-1)]
            public long Length { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this.
        }

        //---------------------------------------------------------------------
        // Instance members

        private HiveProxy                   hive;
        private string                      logFolder;
        private List<XenClient>             xenHosts;
        private SetupController<XenClient>  controller;
        private int                         maxVmNameWidth;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hive">The hive being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public XenServerHostingManager(HiveProxy hive, string logFolder = null)
        {
            this.hive                = hive;
            this.hive.HostingManager = this;
            this.logFolder              = logFolder;
            this.maxVmNameWidth         = hive.Definition.Nodes.Max(n => n.Name.Length) + hive.Definition.Hosting.GetVmNamePrefix(hive.Definition).Length;
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (xenHosts != null)
                {
                    foreach (var xenHost in xenHosts)
                    {
                        xenHost.Dispose();
                    }

                    xenHosts = null;
                }

                GC.SuppressFinalize(this);
            }

            xenHosts = null;
        }

        /// <inheritdoc/>
        public override bool IsProvisionNOP
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public override void Validate(HiveDefinition hiveDefinition)
        {
            // Identify the OSD Bluestore block device for OSD nodes.

            if (hive.Definition.HiveFS.Enabled)
            {
                foreach (var node in hive.Definition.Nodes.Where(n => n.Labels.CephOSD))
                {
                    node.Labels.CephOSDDevice = "/dev/xvdb";
                }
            }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force)
        {
            // $todo(jeff.lill):
            //
            // I'm not implementing [force] here.  I'm not entirely sure
            // that this makes sense for production hives and especially
            // when there are pet nodes.
            //
            // Perhaps it would make more sense to replace this with a
            // [neon hive remove] command.
            //
            //      https://github.com/jefflill/NeonForge/issues/156

            if (IsProvisionNOP)
            {
                // There's nothing to do here.

                return true;
            }

            // Update the node labels with the actual capabilities of the 
            // virtual machines being provisioned.

            foreach (var node in hive.Definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.Labels.PhysicalMachine))
                {
                    node.Labels.PhysicalMachine = node.VmHost;
                }

                if (node.Labels.ComputeCores == 0)
                {
                    node.Labels.ComputeCores = node.GetVmProcessors(hive.Definition);
                }

                if (node.Labels.ComputeRamMB == 0)
                {
                    node.Labels.ComputeRamMB = (int)(node.GetVmMemory(hive.Definition) / NeonHelper.Mega);
                }

                if (node.Labels.StorageCapacityGB == 0)
                {
                    node.Labels.StorageCapacityGB = (int)(node.GetVmDisk(hive.Definition) / NeonHelper.Giga);
                }
            }

            // Build a list of [SshProxy] instances that map to the specified XenServer
            // hosts.  We'll use the [XenClient] instances as proxy metadata.

            var sshProxies = new List<SshProxy<XenClient>>();

            xenHosts = new List<XenClient>();

            foreach (var host in hive.Definition.Hosting.VmHosts)
            {
                var hostAddress  = host.Address;
                var hostname     = host.Name;
                var hostUsername = host.Username ?? hive.Definition.Hosting.VmHostUsername;
                var hostPassword = host.Password ?? hive.Definition.Hosting.VmHostPassword;

                if (string.IsNullOrEmpty(hostname))
                {
                    hostname = host.Address;
                }

                var xenHost = new XenClient(hostAddress, hostUsername, hostPassword, name: host.Name, logFolder: logFolder);

                xenHosts.Add(xenHost);
                sshProxies.Add(xenHost.SshProxy);
            }

            // We're going to provision the XenServer hosts in parallel to
            // speed up hive setup.  This works because each XenServer
            // is essentially independent from the others.

            controller = new SetupController<XenClient>($"Provisioning [{hive.Definition.Name}] hive", sshProxies)
            {
                ShowStatus  = this.ShowStatus,
                MaxParallel = this.MaxParallel
            };

            controller.AddWaitUntilOnlineStep();

            controller.AddStep("sudo config", 
                (node, stepDelay) =>
                {
                    using (var sshClient = node.CloneSshClient())
                    {
                        // We're going to rewrite [/etc/sudoers.d/nopasswd] so that client
                        // connections won't require a TTY and also that SUDO password
                        // prompting will be disabled for all users.
                        //
                        // The file will end up looking like:
                        //
                        //      Defaults !requiretty
                        //      %sudo    ALL=NOPASSWD: ALL

                        var response = sshClient.RunCommand("echo \"Defaults !requiretty\" >> /etc/sudoers.d/nopasswd");

                        if (response.ExitStatus != 0)
                        {
                            node.Fault($"Cannot update [/etc/sudoers.d/nopasswd]: {response.Result}");
                            return;
                        }

                        response = sshClient.RunCommand("echo \"%sudo    ALL=NOPASSWD: ALL\" >> /etc/sudoers.d/nopasswd");

                        if (response.ExitStatus != 0)
                        {
                            node.Fault($"Cannot update [/etc/sudoers.d/nopasswd]: {response.Result}");
                            return;
                        }
                    }
                });

            controller.AddStep("hive folders", (node, stepDelay) => node.CreateHiveHostFolders());
            controller.AddStep("verify readiness", (node, stepDelay) => VerifyReady(node));
            controller.AddStep("virtual machine template", (node, stepDelay) => CheckVmTemplate(node));
            controller.AddStep("provision virtual machines", (node, stepDelay) => ProvisionVirtualMachines(node));
            controller.AddGlobalStep(string.Empty, () => Finish(), quiet: true);

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override string DrivePrefix
        {
            get { return "xvd"; }
        }

        /// <summary>
        /// Returns the list of <see cref="NodeDefinition"/> instances describing which hive
        /// nodes are to be hosted by a specific XenServer.
        /// </summary>
        /// <param name="xenHost">The target XenServer.</param>
        /// <returns>The list of nodes to be hosted on the XenServer.</returns>
        private List<SshProxy<NodeDefinition>> GetHostedNodes(XenClient xenHost)
        {
            var nodeDefinitions = hive.Definition.NodeDefinitions.Values;

            return hive.Nodes.Where(n => n.Metadata.VmHost.Equals(xenHost.Name, StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Returns the name to use when naming the virtual machine hosting the node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(SshProxy<NodeDefinition> node)
        {
            return $"{hive.Definition.Hosting.GetVmNamePrefix(hive.Definition)}{node.Name}";
        }

        /// <summary>
        /// Verify that the XenServer is ready to provision the hive virtual machines.
        /// </summary>
        /// <param name="xenSshProxy">The XenServer SSH proxy.</param>
        private void VerifyReady(SshProxy<XenClient> xenSshProxy)
        {
            // $todo(jeff.lill):
            //
            // It would be nice to verify that XenServer actually has enough 
            // resources (RAM, DISK, and perhaps CPU) here as well.

            var xenHost = xenSshProxy.Metadata;
            var nodes   = GetHostedNodes(xenHost);

            xenSshProxy.Status = "check virtual machines";

            var vmNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vm in xenHost.Machine.List())
            {
                vmNames.Add(vm.NameLabel);
            }

            foreach (var hostedNode in nodes)
            {
                var vmName = GetVmName(hostedNode);

                if (vmNames.Contains(vmName))
                {
                    xenSshProxy.Fault($"XenServer [{xenHost.Name}] already hosts a virtual machine named [{vmName}].");
                    return;
                }
            }
        }

        /// <summary>
        /// Install the virtual machine template on the XenServer if it's not already present.
        /// </summary>
        /// <param name="xenSshProxy">The XenServer SSH proxy.</param>
        private void CheckVmTemplate(SshProxy<XenClient> xenSshProxy)
        {
            var xenHost      = xenSshProxy.Metadata;
            var templateName = hive.Definition.Hosting.XenServer.TemplateName;

            xenSshProxy.Status = "check template";

            if (xenHost.Template.Find(templateName) == null)
            {
                xenSshProxy.Status = "download vm template (slow)";
                xenHost.Template.Install(hive.Definition.Hosting.XenServer.HostXvaUri, templateName, hive.Definition.Hosting.XenServer.StorageRepository);
            }
        }

        /// <summary>
        /// Formats a nice docker node machine status message.
        /// </summary>
        /// <param name="vmName">The name of the virtual machine used to host the hive node.</param>
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
        private void ProvisionVirtualMachines(SshProxy<XenClient> xenSshProxy)
        {
            var xenHost = xenSshProxy.Metadata;

            foreach (var node in GetHostedNodes(xenHost))
            {
                var vmName      = GetVmName(node);
                var processors  = node.Metadata.GetVmProcessors(hive.Definition);
                var memoryBytes = node.Metadata.GetVmMemory(hive.Definition);
                var diskBytes   = node.Metadata.GetVmDisk(hive.Definition);

                xenSshProxy.Status = FormatVmStatus(vmName, "create virtual machine");

                // We need to create a raw drive if the node hosts a Ceph OSD.

                var extraDrives = new List<XenVirtualDrive>();

                if (node.Metadata.Labels.CephOSD)
                {
                    extraDrives.Add(
                        new XenVirtualDrive()
                        {
                            Size = node.Metadata.GetCephOSDDriveSize(hive.Definition)
                        });
                }

                var vm = xenHost.Machine.Create(vmName, hive.Definition.Hosting.XenServer.TemplateName,
                    processors:                 processors,
                    memoryBytes:                memoryBytes,
                    diskBytes:                  diskBytes,
                    snapshot:                   hive.Definition.Hosting.XenServer.Snapshot,
                    extraDrives:                extraDrives,
                    primaryStorageRepository:   hive.Definition.Hosting.XenServer.StorageRepository,
                    extraStorageRespository:    hive.Definition.Hosting.XenServer.OsdStorageRepository);

                xenSshProxy.Status = FormatVmStatus(vmName, "start virtual machine");

                xenHost.Machine.Start(vm);

                // We need to wait for the virtual machine to start and obtain
                // and IP address via DHCP.

                var address = string.Empty;

                xenSshProxy.Status = FormatVmStatus(vmName, "fetch ip address");

                try
                {
                    NeonHelper.WaitFor(
                        () =>
                        {
                            while (true)
                            {
                                vm = xenHost.Machine.Find(vmName);

                                if (!string.IsNullOrEmpty(vm.Address))
                                {
                                    address = vm.Address;
                                    return true;
                                }

                                Thread.Sleep(1000);
                            }
                        },
                        TimeSpan.FromSeconds(120));
                }
                catch (TimeoutException)
                {
                    xenSshProxy.Fault("Timeout waiting for virtual machine to start and set an IP address.");
                }

                // SSH into the VM using the DHCP address, configure the static IP
                // address and extend the primary partition and file system to fill
                // the drive and then reboot.

                var subnet    = NetworkCidr.Parse(hive.Definition.Network.PremiseSubnet);
                var gateway   = hive.Definition.Network.Gateway;
                var broadcast = hive.Definition.Network.Broadcast;

                // We're going to temporarily set the node to the current VM address
                // so we can connect via SSH.

                var savedNodeAddress = node.PrivateAddress;

                try
                {
                    node.PrivateAddress = IPAddress.Parse(address);

                    using (var nodeProxy = hive.GetNode(node.Name))
                    {
                        xenSshProxy.Status = FormatVmStatus(vmName, "connect");
                        nodeProxy.WaitForBoot();

                        // Replace the [/etc/network/interfaces] file to configure the static
                        // IP and then reboot to reinitialize networking subsystem.

                        var primaryInterface = node.GetNetworkInterface(node.PrivateAddress);

                        xenSshProxy.Status = FormatVmStatus(vmName, $"set static ip [{node.PrivateAddress}]");

                        var interfacesText =
$@"# This file describes the network interfaces available on your system
# and how to activate them. For more information, see interfaces(5).

source /etc/network/interfaces.d/*

# The loopback network interface
auto lo
iface lo inet loopback

# The primary network interface
auto {primaryInterface}
iface {primaryInterface} inet static
address {savedNodeAddress}
netmask {subnet.Mask}
gateway {gateway}
broadcast {broadcast}
";
                        nodeProxy.UploadText("/etc/network/interfaces", interfacesText);

                        // Temporarily configure the public Google DNS servers as
                        // the name servers so DNS will work after we reboot with
                        // the static IP.  Note that hive setup will eventually
                        // configure the name servers specified in the hive
                        // definition.

                        // $todo(jeff.lill):
                        //
                        // Is there a good reason why we're not just configuring the
                        // DNS servers from the hive definition here???
                        //
                        // Using the Google DNS seems like it could break some hive
                        // network configurations (e.g. for hives that don't have
                        // access to the public Internet).  Totally private hives
                        // aren't really a supported scenario right now though because
                        // we assume we can use [apt-get]... to pull down packages.

                        var resolvBaseText =
$@"nameserver 8.8.8.8
nameserver 8.8.4.4
";
                        nodeProxy.UploadText("/etc/resolvconf/resolv.conf.d/base", resolvBaseText);

                        // Extend the primary partition and file system to fill 
                        // the virtual the drive. 

                        xenSshProxy.Status = FormatVmStatus(vmName, $"resize primary partition");

                        // $hack(jeff.lill):
                        //
                        // I've seen a transient error here but can't reproduce it.  I'm going
                        // to assume for now that the file system might not be quite ready for
                        // this operation directly after the VM has been rebooted, so we're going
                        // to delay for a few seconds before performing the operations.

                        Thread.Sleep(TimeSpan.FromSeconds(5));
                        nodeProxy.SudoCommand("growpart /dev/xvda 1");
                        nodeProxy.SudoCommand("resize2fs /dev/xvda1");

                        // Reboot to pick up the changes.

                        xenSshProxy.Status = FormatVmStatus(vmName, "reboot");
                        nodeProxy.Reboot(wait: false);
                    }
                }
                finally
                {
                    // Restore the node's IP address.

                    node.PrivateAddress = savedNodeAddress;
                }
            }
        }

        /// <summary>
        /// Perform any necessary global post Hyper-V provisioning steps.
        /// </summary>
        private void Finish()
        {
            // Recreate the node proxies because we disposed them above.
            // We need to do this so subsequent prepare steps will be
            // able to connect to the nodes via the correct addresses.

            hive.CreateNodes();
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: hive.GetNode(nodeName).PrivateAddress.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override void AddPostProvisionSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override void AddPostVpnSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override List<HostedEndpoint> GetPublicEndpoints()
        {
            // Note that public endpoints have to be managed manually for
            // on-premise hive deployments so we're going to return an 
            // empty list.

            return new List<HostedEndpoint>();
        }

        /// <inheritdoc/>
        public override bool CanUpdatePublicEndpoints => false;

        /// <inheritdoc/>
        public override void UpdatePublicEndpoints(List<HostedEndpoint> endpoints)
        {
            // Note that public endpoints have to be managed manually for
            // on-premise hive deployments.
        }
    }
}
