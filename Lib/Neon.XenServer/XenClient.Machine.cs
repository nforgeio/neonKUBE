//-----------------------------------------------------------------------------
// FILE:	    XenClient.Machine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace Neon.XenServer
{
    public partial class XenClient
    {
        /// <summary>
        /// Implements the <see cref="XenClient"/> virtual machine operations.
        /// </summary>
        public class MachineOperations
        {
            private XenClient client;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="client">The XenServer client instance.</param>
            internal MachineOperations(XenClient client)
            {
                this.client = client;
            }

            /// <summary>
            /// Lists the XenServer virtual machines.
            /// </summary>
            /// <returns>The list of virtual machines.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public List<XenVirtualMachine> List()
            {
                var response = client.SafeInvokeItems("vm-list", "params=all");
                var vms      = new List<XenVirtualMachine>();

                foreach (var result in response.Items)
                {
                    vms.Add(new XenVirtualMachine(result));
                }

                return vms;
            }

            /// <summary>
            /// Finds a specific virtual machine by name or unique ID.
            /// </summary>
            /// <param name="name">Specifies the target name.</param>
            /// <param name="uuid">Specifies the target unique ID.</param>
            /// <returns>The named item or <c>null</c> if it doesn't exist.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            /// <remarks>
            /// <note>
            /// One of <paramref name="name"/> or <paramref name="uuid"/> must be specified.
            /// </note>
            /// </remarks>
            public XenVirtualMachine Find(string name = null, string uuid = null)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(uuid));

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return client.Machine.List().FirstOrDefault(item => item.NameLabel == name);
                }
                else if (!string.IsNullOrWhiteSpace(uuid))
                {
                    return client.Machine.List().FirstOrDefault(item => item.Uuid == uuid);
                }
                else
                {
                    throw new ArgumentException($"One of [{nameof(name)}] or [{nameof(uuid)}] must be specified.");
                }
            }

            /// <summary>
            /// <para>
            /// Creates a virtual machine from a template, optionally initializing its memory and 
            /// disk size.
            /// </para>
            /// <note>
            /// This does not start the machine.
            /// </note>
            /// </summary>
            /// <param name="name">Name for the new virtual machine.</param>
            /// <param name="templateName">Identifies the template.</param>
            /// <param name="processors">Optionally specifies the number of processors to assign.  This defaults to <b>2</b>.</param>
            /// <param name="memoryBytes">Optionally specifies the memory assigned to the machine (overriding the template).</param>
            /// <param name="diskBytes">Optionally specifies the disk assigned to the machine (overriding the template).</param>
            /// <param name="snapshot">Optionally specifies that the virtual machine should snapshot the template.  This defaults to <c>false</c>.</param>
            /// <param name="extraDisks">
            /// Optionally specifies any additional virtual disks to be created and 
            /// then attached to the new virtual machine.
            /// </param>
            /// <param name="primaryStorageRepository">
            /// Optionally specifies the storage repository where the virtual machine's
            /// primary disk will be created.  This defaults to <b>Local storage</b>.
            /// </param>
            /// <param name="extraStorageRespository">
            /// Optionally specifies the storage repository where any extra disks for
            /// the virtual machine will be created.  This defaults to <b>Local storage</b>.
            /// <note>
            /// The default value assumes that your XenServer pool is <b>NOT CONFIGURED FOR HA</b>.
            /// Auto start VMs are not recommended for HA pools due to potential conflicts.  We're
            /// not sure what problems having autostart VMs in a HA pool cause.
            /// </note>
            /// </param>
            /// <returns>The new <see cref="XenVirtualMachine"/>.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            /// <remarks>
            /// <note>
            /// <paramref name="snapshot"/> is ignored if the virtual machine template is not 
            /// hosted by the same storage repository where the virtual machine is to be created.
            /// </note>
            /// </remarks>
            public XenVirtualMachine Create(
                string                          name, 
                string                          templateName, 
                int                             processors               = 2, 
                long                            memoryBytes              = 0, 
                long                            diskBytes                = 0, 
                bool                            snapshot                 = false,
                IEnumerable<XenVirtualDisk>     extraDisks               = null,
                string                          primaryStorageRepository = "Local storage",
                string                          extraStorageRespository  = "Local storage")
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(templateName), nameof(templateName));
                Covenant.Requires<ArgumentException>(processors > 0, nameof(processors));
                Covenant.Requires<ArgumentException>(memoryBytes >= 0, nameof(memoryBytes));
                Covenant.Requires<ArgumentException>(diskBytes >= 0, nameof(diskBytes));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(primaryStorageRepository), nameof(primaryStorageRepository));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(extraStorageRespository), nameof(extraStorageRespository));

                if (client.Template.Find(templateName) == null)
                {
                    throw new XenException($"Template [{templateName}] does not exist.");
                }

                // We need to determine whether the VM template is persisted to the same storage
                // repository as the desired target for the VM.  If the storage repos are the same,
                // we won't pass the [sr-uuid] parameter so that XenServer will do a fast clone.
                // This is necessary because the xe command looks like it never does a fast clone
                // when [sr-uuid] is passed, even if the template and target VM storage repositories
                // are the same.
                //
                //      https://github.com/nforgeio/neonKUBE/issues/326
                //
                // Unfortunately, there doesn't appear to be a clean way to inspect a VM template 
                // to list its disks and determine where they live.  So we'll list the virtual disk
                // interfaces and look for the disk named like:
                //
                //      <template-name> 0
                //
                // where <template-name> identifies the template and "0" indicates the disk number.
                //
                // NOTE: This assumes that cluster VM templates have only a single disk, which
                //       will probably always be the case since we only add disks after the VMs
                //       are created.

                var primarySR       = client.Repository.Find(name: primaryStorageRepository, mustExist: true);
                var vdiListResponse = client.InvokeItems("vdi-list");
                var templateVdiName = $"{templateName} 0";
                var templateSrUuid  = (string)null;
                var srUuidArg       = (string)null;

                foreach (var vdiProperties in vdiListResponse.Items)
                {
                    if (vdiProperties["name-label"] == templateVdiName)
                    {
                        templateSrUuid = vdiProperties["sr-uuid"];
                        break;
                    }
                }

                if (!snapshot || templateSrUuid != primarySR.Uuid)
                {
                    srUuidArg = $"sr-uuid={primarySR.Uuid}";
                }

                // Create the VM.

                var vmInstallResponse = client.SafeInvoke("vm-install", $"template={templateName}", $"new-name-label={name}", srUuidArg);
                var vmUuid            = vmInstallResponse.OutputText.Trim();

                // Configure processors

                client.SafeInvoke("vm-param-set",
                    $"uuid={vmUuid}",
                    $"VCPUs-at-startup={processors}",
                    $"VCPUs-max={processors}");

                // Citrix says that VM autostart is not compatible with HA so we don't
                // want to enable autostart when HA is enabled.  We'll assume that the
                // user will configure autostart manually via the HA settings.
                //
                // If the XenServer host is not HA enabled, we're going to configure
                // the VM to start automatically when the host machine boots.  We're
                // going to list the XenServer pool to obtain its UUID and then inspect
                // its parameters to determine whether HA is enabled.  We're going to
                // assume that any single XenServer host can only be a member of a
                // single pool (which makes sense).
                //
                // Note that the pool list will look like:
                //
                //   uuid ( RO)                : 55ab0faf-19e2-6a93-717d-441213705f60
                //             name-label ( RW):
                //       name-description ( RW):
                //                 master ( RO): eae75dd5-6ae2-474f-a04d-a982d52821e7
                //             default-SR ( RW): 62166a25-0601-dc07-d1ce-e74625e71444
                //
                // and the pool parameters will look like:
                //
                //   uuid ( RO)                            : 55ab0faf-19e2-6a93-717d-441213705f60
                //                         name-label ( RW): test
                //                   name-description ( RW):
                //                             master ( RO): eae75dd5-6ae2-474f-a04d-a982d52821e7
                //                         default-SR ( RW): <not in database>
                //                      crash-dump-SR ( RW): <not in database>
                //                   suspend-image-SR ( RW): <not in database>
                //                 supported-sr-types ( RO): smb; lvm; iso; nfs; lvmofcoe; udev; hba; dummy; ext; lvmoiscsi; lvmohba; file; iscsi
                //                       other-config (MRW): auto_poweron: true; memory-ratio-hvm: 0.25; memory-ratio-pv: 0.25
                //                 allowed-operations (SRO): cluster_create; ha_enable
                //                 current-operations (SRO):
                //                         ha-enabled ( RO): false
                //                   ha-configuration ( RO):
                //                      ha-statefiles ( RO):
                //       ha-host-failures-to-tolerate ( RW): 0
                //                 ha-plan-exists-for ( RO): 0
                //                ha-allow-overcommit ( RW): false
                //                   ha-overcommitted ( RO): false
                //                              blobs ( RO):
                //                            wlb-url ( RO):
                //                       wlb-username ( RO):
                //                        wlb-enabled ( RW): false
                //                    wlb-verify-cert ( RW): false
                //              igmp-snooping-enabled ( RW): false
                //                         gui-config (MRW):
                //                health-check-config (MRW):
                //                       restrictions ( RO): restrict_vswitch_controller: false; restrict_lab: false; restrict_stage: false; restrict_storagelink: false; restrict_storagelink_site_recovery: false; restrict_web_selfservice: false; restrict_web_selfservice_manager: false; restrict_hotfix_apply: false; restrict_export_resource_data: false; restrict_read_caching: false; restrict_cifs: false; restrict_health_check: false; restrict_xcm: false; restrict_vm_memory_introspection: false; restrict_batch_hotfix_apply: false; restrict_management_on_vlan: false; restrict_ws_proxy: false; restrict_vlan: false; restrict_qos: false; restrict_pool_attached_storage: false; restrict_netapp: false; restrict_equalogic: false; restrict_pooling: false; enable_xha: true; restrict_marathon: false; restrict_email_alerting: false; restrict_historical_performance: false; restrict_wlb: false; restrict_rbac: false; restrict_dmc: false; restrict_checkpoint: false; restrict_cpu_masking: false; restrict_connection: false; platform_filter: false; regular_nag_dialog: false; restrict_vmpr: false; restrict_vmss: false; restrict_intellicache: false; restrict_gpu: false; restrict_dr: false; restrict_vif_locking: false; restrict_storage_xen_motion: false; restrict_vgpu: false; restrict_integrated_gpu_passthrough: false; restrict_vss: false; restrict_guest_agent_auto_update: false; restrict_pci_device_for_auto_update: false; restrict_xen_motion: false; restrict_guest_ip_setting: false; restrict_ad: false; restrict_ssl_legacy_switch: false; restrict_nested_virt: false; restrict_live_patching: false; restrict_set_vcpus_number_live: false; restrict_pvs_proxy: false; restrict_igmp_snooping: false; restrict_rpu: false; restrict_pool_size: false; restrict_cbt: false; restrict_usb_passthrough: false; restrict_network_sriov: false; restrict_corosync: true; restrict_zstd_export: false
                //                               tags (SRW):
                //                      license-state ( RO): edition: xcp-ng; expiry: never
                //                   ha-cluster-stack ( RO): xhad
                //                 guest-agent-config (MRW):
                //                           cpu_info (MRO): features_hvm_host: 1fcbfbff-80b82221-2993fbff-00000403-00000000-00000000-00000000-00000000-00001000-9c000000-00000000-00000000-00000000-00000000-00000000; features_hvm: 1fcbfbff-80b82221-2993fbff-00000403-00000000-00000000-00000000-00000000-00001000-9c000000-00000000-00000000-00000000-00000000-00000000; features_pv_host: 1fc9cbf5-80b82201-2991cbf5-00000003-00000000-00000000-00000000-00000000-00001000-8c000000-00000000-00000000-00000000-00000000-00000000; features_pv: 1fc9cbf5-80b82201-2991cbf5-00000003-00000000-00000000-00000000-00000000-00001000-8c000000-00000000-00000000-00000000-00000000-00000000; socket_count: 2; cpu_count: 16; vendor: GenuineIntel
                //            policy-no-vendor-device ( RW): false
                //             live-patching-disabled ( RW): false

                var poolList = client.SafeInvokeItems("pool-list").Items.First();
                var poolUuid = poolList["uuid"];
                var pool     = client.SafeInvokeItems("pool-param-list", $"uuid={poolUuid}").Items.First();

                if (pool["ha-enabled"] == "false")
                {
                    client.SafeInvoke("pool-param-set",
                        $"uuid={poolUuid}",
                        $"other-config:auto_poweron=true");

                    client.SafeInvoke("vm-param-set",
                        $"uuid={vmUuid}",
                        $"other-config:auto_poweron=true");
                }

                // Configure memory.

                if (memoryBytes > 0)
                {
                    client.SafeInvoke("vm-memory-limits-set",
                        $"uuid={vmUuid}",
                        $"dynamic-max={memoryBytes}",
                        $"dynamic-min={memoryBytes}",
                        $"static-max={memoryBytes}",
                        $"static-min={memoryBytes}");
                }

                // Configure the primary disk.

                if (diskBytes > 0)
                {
                    var disks = client.SafeInvokeItems("vm-disk-list", $"vm={vmUuid}").Items;
                    var vdi   = disks.FirstOrDefault(properties => properties.ContainsKey("Disk 0 VDI"));

                    if (vdi == null)
                    {
                        throw new XenException($"Cannot locate disk [0] for [{name}] virtual machine.");
                    }

                    var vdiUuid = vdi["uuid"];

                    client.SafeInvoke("vdi-resize", $"uuid={vdiUuid}", $"disk-size={diskBytes}");

                    // Rename the disk to "operating system".

                    client.SafeInvoke("vdi-param-set", $"uuid={vdiUuid}", $"name-label={name}: OS disk", $"name-description=Operating system");
                }

                // Configure any additional disks.

                if (extraDisks != null && extraDisks.Count() > 0)
                {
                    var diskIndex = 1; // The boot disk has index=0 so we'll skip that.
                    var extraSR   = client.Repository.Find(name: extraStorageRespository, mustExist: true);

                    foreach (var disk in extraDisks)
                    {
                        // Create the disk.

                        client.SafeInvoke("vm-disk-add", $"uuid={vmUuid}", $"sr-uuid={extraSR.Uuid}", $"disk-size={disk.Size}", $"device={diskIndex}");

                        // Set the disk's name and description.

                        var disks = client.SafeInvokeItems("vm-disk-list", $"vm={vmUuid}").Items;
                        var vdi   = disks.FirstOrDefault(properties => properties.ContainsKey("name-label") && properties["name-label"] == "Created by xe");

                        if (vdi == null)
                        {
                            throw new XenException($"Cannot locate the new node [{disk.Name}] disk.");
                        }

                        var vdiUuid = vdi["uuid"];

                        var diskName        = disk.Name ?? "disk";
                        var diskDescription = disk.Description ?? string.Empty;

                        client.SafeInvoke("vdi-param-set", $"uuid={vdiUuid}", $"name-label={diskName}", $"name-description={diskDescription}");

                        diskIndex++;
                    }
                }

                return client.Machine.Find(uuid: vmUuid);
            }

            /// <summary>
            /// Starts a virtual machine.
            /// </summary>
            /// <param name="virtualMachine">The target virtual machine.</param>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public void Start(XenVirtualMachine virtualMachine)
            {
                Covenant.Requires<ArgumentNullException>(virtualMachine != null, nameof(virtualMachine));

                client.SafeInvoke("vm-start", $"uuid={virtualMachine.Uuid}");
            }

            /// <summary>
            /// Shuts down a virtual machine.
            /// </summary>
            /// <param name="virtualMachine">The target virtual machine.</param>
            /// <param name="force">Optionally forces the virtual machine to shutdown.</param>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public void Shutdown(XenVirtualMachine virtualMachine, bool force = false)
            {
                Covenant.Requires<ArgumentNullException>(virtualMachine != null, nameof(virtualMachine));

                if (force)
                {
                    client.SafeInvoke("vm-shutdown", $"uuid={virtualMachine.Uuid}", "--force");
                }
                else
                {
                    client.SafeInvoke("vm-shutdown", $"uuid={virtualMachine.Uuid}");
                }
            }

            /// <summary>
            /// Reboots a virtual machine.
            /// </summary>
            /// <param name="virtualMachine">The target virtual machine.</param>
            /// <param name="force">Optionally forces the virtual machine to reboot.</param>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public void Reboot(XenVirtualMachine virtualMachine, bool force = false)
            {
                Covenant.Requires<ArgumentNullException>(virtualMachine != null, nameof(virtualMachine));

                if (force)
                {
                    client.SafeInvoke("vm-reboot", $"uuid={virtualMachine.Uuid}", "--force");
                }
                else
                {
                    client.SafeInvoke("vm-reboot", $"uuid={virtualMachine.Uuid}");
                }
            }

            /// <summary>
            /// Removes a virtual machine and its drives.
            /// </summary>
            /// <param name="virtualMachine">The target virtual machine.</param>
            /// <param name="noDriveRemoval">Optionally prevents the VM drives from being removed.</param>
            public void Remove(XenVirtualMachine virtualMachine, bool noDriveRemoval = false)
            {
                Covenant.Requires<ArgumentNullException>(virtualMachine != null, nameof(virtualMachine));

                client.SafeInvoke("vm-reset-powerstate", $"uuid={virtualMachine.Uuid}", "--force");

                if (noDriveRemoval)
                {
                    client.SafeInvoke("vm-destroy", $"uuid={virtualMachine.Uuid}");
                }
                else
                {
                    client.SafeInvoke("vm-uninstall", $"uuid={virtualMachine.Uuid}");
                }
            }

            /// <summary>
            /// <para>
            /// Adds a new disk to a virtual machine.
            /// </para>
            /// <note>
            /// The virtual machine must be stopped.
            /// </note>
            /// </summary>
            /// <param name="virtualMachine">The target virtual machine.</param>
            /// <param name="disk">The disk information.</param>
            public void AddDisk(XenVirtualMachine virtualMachine, XenVirtualDisk disk)
            {
                Covenant.Requires<ArgumentNullException>(virtualMachine != null, nameof(virtualMachine));
                Covenant.Requires<ArgumentNullException>(disk != null, nameof(disk));

                var vmDisks   = client.SafeInvokeItems("vm-disk-list", $"vm={virtualMachine.Uuid}").Items;
                var diskIndex = vmDisks.Count(disk => disk.TryGetValue("userdevice", out var device));  // Count only VDB (virtual block devices)
                var extraSR   = client.Repository.Find(name: disk.StorageRepository, mustExist: true);

                // Create the disk.

                client.SafeInvoke("vm-disk-add", $"uuid={virtualMachine.Uuid}", $"sr-uuid={extraSR.Uuid}", $"disk-size={disk.Size}", $"device={diskIndex}");

                // Set the disk's name and description.

                vmDisks = client.SafeInvokeItems("vm-disk-list", $"vm={virtualMachine.Uuid}").Items;

                var vdi = vmDisks.FirstOrDefault(properties => properties.ContainsKey("name-label") && properties["name-label"] == "Created by xe");

                if (vdi == null)
                {
                    throw new XenException($"Cannot locate the new node [{disk.Name}] disk.");
                }

                var vdiUuid = vdi["uuid"];

                var diskName        = disk.Name ?? "disk";
                var diskDescription = disk.Description ?? string.Empty;

                client.SafeInvoke("vdi-param-set", $"uuid={vdiUuid}", $"name-label={diskName}", $"name-description={diskDescription}");
            }

            /// <summary>
            /// Returns the number of disks attached to a virtual machine.
            /// </summary>
            /// <param name="virtualMachine">The target virtual machine.</param>
            /// <returns>The number of attached disks.</returns>
            public int DiskCount(XenVirtualMachine virtualMachine)
            {
                Covenant.Requires<ArgumentNullException>(virtualMachine != null, nameof(virtualMachine));

                var vmDisks = client.SafeInvokeItems("vm-disk-list", $"vm={virtualMachine.Uuid}").Items;

                // Count only VDB (virtual block devices) so we don't double count any disks.

                return vmDisks.Count(disk => disk.TryGetValue("userdevice", out var device));
            }
        }
    }
}
