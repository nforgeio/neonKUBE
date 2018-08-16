//-----------------------------------------------------------------------------
// FILE:	    XenVirtualMachine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;

using Neon.Common;
using Neon.Hive;

namespace Neon.Xen
{
    /// <summary>
    /// Describes a XenServer virtual machine.
    /// </summary>
    public class XenVirtualMachine : XenObject
    {
        /// <summary>
        /// Constructs an instance from raw property values returned by the <b>xe CLI</b>.
        /// </summary>
        /// <param name="rawProperties">The raw object properties.</param>
        internal XenVirtualMachine(IDictionary<string, string> rawProperties)
            : base(rawProperties)
        {
            this.Uuid       = rawProperties["uuid"];
            this.NameLabel  = rawProperties["name-label"];
            this.PowerState = rawProperties["power-state"];

            // We're only going to explicitly support one network interface,
            // interface 0.  We're going to attempt extracting the IP address
            // from the [networks] property which will probabvly look something
            // like this"
            //
            //      networks (MRO): 0/ip: 10.50.0.236; 0/ipv6/0: fe80::46f:19ff:fe95:a5d2

            if (rawProperties.TryGetValue("networks", out var networks))
            {
                var pattern = "0/ip: ";
                var pos     = networks.IndexOf(pattern);

                if (pos != -1)
                {
                    pos += pattern.Length;

                    var posEnd = networks.IndexOf(";", pos);

                    if (posEnd != -1)
                    {
                        Address = networks.Substring(pos, posEnd - pos).Trim();
                    }
                    else
                    {
                        Address = networks.Substring(pos).Trim();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the repository unique ID.
        /// </summary>
        public string Uuid { get; private set; }

        /// <summary>
        /// Returns the repository name.
        /// </summary>
        public string NameLabel { get; private set; }

        /// <summary>
        /// Returns the repository description.
        /// </summary>
        public string PowerState { get; private set; }

        /// <summary>
        /// Returns the IP address associated with the VM or <c>null</c>
        /// if the VM is not running or hasn't obtained an address yet.
        /// </summary>
        public string Address { get; private set; }

        /// <summary>
        /// Indicates whether the virtual machine is running.
        /// </summary>
        public bool IsRunning => PowerState == "running";
    }
}
