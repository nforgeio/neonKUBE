//-----------------------------------------------------------------------------
// FILE:	    XenClient.VirtualMachine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Cluster;
using Neon.Common;

namespace Neon.Cluster.XenServer
{
    public partial class XenClient
    {
        /// <summary>
        /// Implements the <see cref="XenClient"/> virtual machine template operations.
        /// </summary>
        public class StorageVirtualMachineOperations
        {
            private XenClient client;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="client">The XenServer client instance.</param>
            internal StorageVirtualMachineOperations(XenClient client)
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
                    return client.VirtualMachine.List().FirstOrDefault(item => item.NameLabel == name);
                }
                else if (!string.IsNullOrWhiteSpace(uuid))
                {
                    return client.VirtualMachine.List().FirstOrDefault(item => item.Uuid == uuid);
                }
                else
                {
                    throw new ArgumentException($"One of [{nameof(name)}] or [{nameof(uuid)}] must be specified.");
                }
            }

            /// <summary>
            /// Creates a virtual machine from a template.
            /// </summary>
            /// <param name="name">Name for the new virtual machine.</param>
            /// <param name="templateName">Identifies the template.</param>
            /// <returns>The new <see cref="XenVirtualMachine"/>.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public XenVirtualMachine Install(string name, string templateName)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(templateName));

                if (client.Template.Find(templateName) == null)
                {
                    throw new XenException($"Template [{templateName}] does not exist.");
                }

                var response = client.SafeInvoke("vm-install", $"template={templateName}", $"new-name-label={name}");
                var uuid     = response.OutputText.Trim();

                return client.VirtualMachine.Find(uuid: uuid);
            }

            /// <summary>
            /// Starts a virtual machine.
            /// </summary>
            /// <param name="virtualMachine">The target virtual machine.</param>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public void Start(XenVirtualMachine virtualMachine)
            {
                Covenant.Requires<ArgumentNullException>(virtualMachine != null);

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
                Covenant.Requires<ArgumentNullException>(virtualMachine != null);

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
                Covenant.Requires<ArgumentNullException>(virtualMachine != null);

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
