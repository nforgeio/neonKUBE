//-----------------------------------------------------------------------------
// FILE:	    XenServerClusters.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace Neon.Kube.Xunit
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> <b>XenServer</b> cluster definitions used by maintainers for unit test clusters deployed by <see cref="ClusterFixture"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class acts as repostory for cluster definitions that can be deployed by unit tests
    /// via <see cref="ClusterFixture"/>.  These currently work only for project maintainers who
    /// have installed the <b>neon-assistant</b> tool and have configured their profile there
    /// as well as in 1Password.  The definitions are organized by hosting environment. 
    /// </para>
    /// <para><b>COMMON PROFILE REQUIREMENTS</b></para>
    /// <para>
    /// These profile values must be configured in <b>neon-assistant</b> for all test clusters.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>lan.gateway</b> (profile)</term>
    ///     <description>
    ///     Specifies the default gateway IPv4 address for the LAN. 
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>lan.subnet</b> (profile)</term>
    ///     <description>
    ///     Specifies the CIDR for the LAN.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>lan.dns0</b> (profile)</term>
    ///     <description>
    ///     Specifies the IPv4 address for the primary local DNS server.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>lan.dns1</b> (profile)</term>
    ///     <description>
    ///     Specifies the IPv4 address for the secondary local DNS server.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>datacenter</b> (profile)</term>
    ///     <description>
    ///     Identifies the datacenter where the clusters will be hosted.
    ///     </description>
    /// </item>
    /// </list>
    /// <para><b>XENSERVER/XCP-ng</b></para>
    /// <para>
    /// These clusters are hosted on a single XenServer specified by the <b>neon-assistant</b>
    /// profile along with other required common settings: 
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>xen-testhost</b> (profile)</term>
    ///     <description>
    ///     Specifies the IPv4 address for the XenServer/XCP-ng server where
    ///     the cluster nodes will be provisioned.  Each machine that runs tests
    ///     will need to specify a unique host machine to avoid conflicts.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>XENSERVER_LOGIN</b> (1Password)</term>
    ///     <description>
    ///     The 1Password password credentials for the XenServer host machine.
    ///     These credentials are currently shared across all XenServer deployments,
    ///     but this may change in the future.
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// Each cluster definition also uses the neon-assistant profile to obtain
    /// the IP addresses to be assigned to the cluster nodes.
    /// </para>
    /// <para><b>XEN-TINY</b></para>
    /// <para>
    /// The following profile definitions assigning IPv4 addresses to nodes are
    /// required for the <see cref="Tiny"/> cluster.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>xenserver.tiny0.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the single cluster node.
    ///     </description>
    /// </item>
    /// </list>
    /// <para><b>XEN-SMALL</b></para>
    /// <para>
    /// The following profile definitions assigning IPv4 addresses to nodes are
    /// required for the <see cref="Small"/> cluster.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>xenserver.small0.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the cluster control-plane node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>xenserver.small1.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the first cluster worker node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>xenserver.small2.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the second cluster worker node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>xenserver.small3.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the third cluster worker node.
    ///     </description>
    /// </item>
    /// </list>
    /// <para><b>XEN-LARGE</b></para>
    /// <para>
    /// The following profile definitions assigning IPv4 addresses to nodes are
    /// required for the <see cref="Large"/> cluster.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>xenserver.large0.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the first cluster control-plane node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>xenserver.large1.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the second cluster control-plane node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>xenserver.large2.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the third cluster control-plane node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>xenserver.large3.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the first cluster worker node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>xenserver.large4.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the second cluster worker node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>xenserver.large5.ip</b></term>
    ///     <description>
    ///     IPv4 address to be assigned to the third cluster worker node.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public static class XenServerClusters
    {
        /// <summary>
        /// <b>XenServer:</b> single node cluster definition.
        /// </summary>
        public const string Tiny = @"
name: xenserver-tiny
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: xenserver
  vm:
    hostUsername: $<secret:XENSERVER_LOGIN[username]>
    hostPassword: $<secret:XENSERVER_LOGIN[password]>
    namePrefix: test-tiny
    cores: 4
    memory: 16 GiB
    osDisk: 64 GiB
    openEbsDisk: 32 GiB
    hosts:
    - name: XEN-TEST
      address: $<profile:xen-test.host>
  xenServer:
     snapshot: true
network:
  premiseSubnet: $<profile:lan.subnet>
  gateway: $<profile:lan.gateway>
  nameservers:
  - $<profile:lan.dns0>
  - $<profile:lan.dns1>
nodes:
  control-0:
    role: control-plane
    address: $<profile:xenserver.tiny0.ip>
    vm:
      host: XEN-TEST
";

        /// <summary>
        /// <b>XenServer:</b> 1 control-plane and 3 worker cluster definition.
        /// </summary>
        public const string Small = @"
name: xenserver-small
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: false
hosting:
  environment: xenserver
  vm:
    hostUsername: $<secret:XENSERVER_LOGIN[username]>
    hostPassword: $<secret:XENSERVER_LOGIN[password]>
    namePrefix: test-small
    cores: 4
    memory: 16 GiB
    osDisk: 64 GiB
    openEbsDisk: 32 GiB
    hosts:
    - name: XEN-TEST
      address: $<profile:xen-test.host>
  xenServer:
     snapshot: true
network:
  premiseSubnet: $<profile:lan.subnet>
  gateway: $<profile:lan.gateway>
  nameservers:
  - $<profile:lan.dns0>
  - $<profile:lan.dns1>
nodes:
  control-0:
    role: control-plane
    address: $<profile:xenserver.small0.ip>
    vm:
      host: XEN-TEST
  worker-0:
    role: worker
    address: $<profile:xenserver.small1.ip>
    vm:
      host: XEN-TEST
  worker-1:
    role: worker
    address: $<profile:xenserver.small2.ip>
    vm:
      host: XEN-TEST
  worker-2:
    role: worker
    address: $<profile:xenserver.small3.ip>
    vm:
      host: XEN-TEST
";

        /// <summary>
        /// <b>XenServer:</b> 3 control-plane and 3 worker cluster definition.
        /// </summary>
        public const string Large = @"
name: xenserver-large
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: false
hosting:
  environment: xenserver
  vm:
    hostUsername: $<secret:XENSERVER_LOGIN[username]>
    hostPassword: $<secret:XENSERVER_LOGIN[password]>
    namePrefix: test-large
    cores: 4
    memory: 16 GiB
    osDisk: 64 GiB
    openEbsDisk: 32 GiB
    hosts:
    - name: XEN-TEST
      address: $<profile:xen-test.host>
  xenServer:
     snapshot: true
network:
  premiseSubnet: $<profile:lan.subnet>
  gateway: $<profile:lan.gateway>
  nameservers:
  - $<profile:lan.dns0>
  - $<profile:lan.dns1>
nodes:
  control-0:
    role: control-plane
    address: $<profile:xenserver.large0.ip>
    memory: 4 GiB
    vm:
      host: XEN-TEST
  control-1:
    role: control-plane
    address: $<profile:xenserver.large1.ip>
    memory: 4 GiB
    vm:
      host: XEN-TEST
  control-2:
    role: control-plane
    address: $<profile:xenserver.large2.ip>
    memory: 4 GiB
    vm:
      host: XEN-TEST
  worker-0:
    role: worker
    address: $<profile:xenserver.large3.ip>
     vm:
       host: XEN-TEST
  worker-1:
    role: worker
    address: $<profile:xenserver.large4.ip>
    vm:
      host: XEN-TEST
  worker-2:
    role: worker
    address: $<profile:xenserver.large5.ip>
    vm:
      host: XEN-TEST
";
    }
}
