//-----------------------------------------------------------------------------
// FILE:	    XenServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Linq;

using Neon.Common;
using Neon.XenServer;

namespace Neon.Deployment
{
    /// <summary>
    /// XenServer/XCP-ng related deployment utilities.
    /// </summary>
    public static class XenServer
    {
        /// <summary>
        /// Connects to a XenServer/XCP-ng host and removes any VMs matching the name or file
        /// wildcard pattern, forceably shutting the VMs down when necessary.  Note that the
        /// VM's drives will also be removed.
        /// </summary>
        /// <param name="addressOrFQDN">Specifies the IP address or hostname for the target XenServer host machine.</param>
        /// <param name="username">Specifies the username to be used to connect to the host.</param>
        /// <param name="password">Specifies the host password.</param>
        /// <param name="nameOrPattern">Specifies the VM name or pattern including '*' or '?' wildcards to be used to remove VMs.</param>
        public static void RemoveVMs(string addressOrFQDN, string username, string password, string nameOrPattern)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(addressOrFQDN), nameof(addressOrFQDN));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username), nameof(username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(password), nameof(password));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrPattern), nameof(nameOrPattern));

            var nameRegEx = NeonHelper.FileWildcardRegex(nameOrPattern);

            using (var client = new XenClient(addressOrFQDN, username, password))
            {
                foreach (var vm in client.Machine.List()
                    .Where(vm => nameRegEx.IsMatch(vm.NameLabel)))
                {
                    if (vm.IsRunning)
                    {
                        client.Machine.Shutdown(vm, turnOff: true);
                    }

                    client.Machine.Remove(vm, keepDrives: false);
                }
            }
        }
    }
}
