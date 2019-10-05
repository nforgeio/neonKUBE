//-----------------------------------------------------------------------------
// FILE:	    XenClient.Machine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Xen
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

                foreach (var result in response.Results)
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
            /// <param name="extraDrives">
            /// Optionally specifies any additional virtual drives to be created and 
            /// then attached to the new virtual machine (e.g. for Ceph OSD).
            /// </param>
            /// <param name="primaryStorageRepository">
            /// Optionally specifies the storage repository where the virtual machine's
            /// primary disk will be created.  This defaults to <b>Local storage</b>.
            /// </param>
            /// <param name="extraStorageRespository">
            /// Optionally specifies the storage repository where any extra drives for
            /// the virtual machine will be created.  This defaults to <b>Local storage</b>.
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
                int                             processors  = 2, 
                long                            memoryBytes = 0, 
                long                            diskBytes   = 0, 
                bool                            snapshot    = false,
                IEnumerable<XenVirtualDrive>    extraDrives = null,
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

                foreach (var vdiProperties in vdiListResponse.Results)
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
                var uuid              = vmInstallResponse.OutputText.Trim();

                // Configure processors

                client.SafeInvoke("vm-param-set",
                    $"uuid={uuid}",
                    $"VCPUs-at-startup={processors}",
                    $"VCPUs-max={processors}");

                // Configure memory.

                if (memoryBytes > 0)
                {
                    client.SafeInvoke("vm-memory-limits-set",
                        $"uuid={uuid}",
                        $"dynamic-max={memoryBytes}",
                        $"dynamic-min={memoryBytes}",
                        $"static-max={memoryBytes}",
                        $"static-min={memoryBytes}");
                }

                // Configure the primary disk.

                if (diskBytes > 0)
                {
                    var disks = client.SafeInvokeItems("vm-disk-list", $"uuid={uuid}").Results;
                    var vdi   = disks.FirstOrDefault(items => items.ContainsKey("Disk 0 VDI"));

                    if (vdi == null)
                    {
                        throw new XenException($"Cannot locate disk for [{name}] virtual machine.");
                    }

                    var vdiUuid = vdi["uuid"];

                    client.SafeInvoke("vdi-resize", $"uuid={vdiUuid}", $"disk-size={diskBytes}");
                }

                // Configure any additional disks.

                if (extraDrives != null && extraDrives.Count() > 0)
                {
                    var driveIndex = 1; // The boot device has index=0
                    var extraSR    = client.Repository.Find(name: extraStorageRespository, mustExist: true);

                    foreach (var drive in extraDrives)
                    {
                        client.SafeInvoke("vm-disk-add", $"uuid={uuid}", $"sr-uuid={extraSR.Uuid}", $"disk-size={drive.Size}", $"device={driveIndex}");
                        driveIndex++;
                    }
                }

                return client.Machine.Find(uuid: uuid);
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
        }
    }
}
