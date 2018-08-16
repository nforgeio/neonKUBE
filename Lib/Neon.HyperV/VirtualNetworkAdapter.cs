//-----------------------------------------------------------------------------
// FILE:	    VirtualNetworkAdapter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.HyperV
{
    /// <summary>
    /// Describes a Hyper-V virtual network adapter attached to a virtual machine.
    /// </summary>
    public class VirtualNetworkAdapter
    {
        /// <summary>
        /// The adapter name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// <c>true</c> if this adapter is attached to the management operating system.
        /// </summary>
        public bool IsManagementOs { get; set; }

        /// <summary>
        /// The name of the attached virtual machine.
        /// </summary>
        public string VMName { get; set; }

        /// <summary>
        /// The attached switch name.
        /// </summary>
        public string SwitchName { get; set; }

        /// <summary>
        /// The adapter's MAC address.
        /// </summary>
        public string MacAddress { get; set; }

        /// <summary>
        /// The adapter status.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The IP addresses assigned to the adapter.
        /// </summary>
        public List<IPAddress> Addresses { get; set; } = new List<IPAddress>();
    }
}
