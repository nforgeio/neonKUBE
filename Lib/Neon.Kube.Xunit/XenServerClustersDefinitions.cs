//-----------------------------------------------------------------------------
// FILE:        XenServerClusterDefinitions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using Amazon.EC2.Model;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace Neon.Kube.Xunit
{
    /// <summary>
    /// <b>MAINTAINERS ONLY:</b> <b>XenServer</b> cluster definitions used by maintainers for
    /// unit test clusters deployed by <see cref="ClusterFixture"/> on <b>XenServer</b>.
    /// </summary>
    public static class XenServerClustersDefinitions
    {
        private static string Preprocess(string input)
        {
            using (var reader = new PreprocessReader(new StringReader(input)))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Cluster definition that deployes a single node on XenServer.
        /// </summary>
        public static string Tiny
        {
            get
            {
                const string clusterDefinition = @"
name: test-xenserver-tiny
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: xenserver
  hypervisor:
    hostUsername: $<secret:XENSERVER_LOGIN[username]>
    hostPassword: $<secret:XENSERVER_LOGIN[password]>
    namePrefix: test-tiny
    vcpus: 4
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
    hypervisor:
      host: XEN-TEST
";
                return Preprocess(clusterDefinition);
            }
        }

        /// <summary>
        /// Cluster definition that deploys a cluster with 1 control-plane and 3 workers on XenServer.
        /// </summary>
        public static string Small
        {
            get
            {
                const string clusterDefinition = @"
name: test-xenserver-small
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: false
hosting:
  environment: xenserver
  hypervisor:
    hostUsername: $<secret:XENSERVER_LOGIN[username]>
    hostPassword: $<secret:XENSERVER_LOGIN[password]>
    namePrefix: test-small
    vcpus: 4
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
    hypervisor:
      host: XEN-TEST
  worker-0:
    role: worker
    address: $<profile:xenserver.small1.ip>
    hypervisor:
      host: XEN-TEST
  worker-1:
    role: worker
    address: $<profile:xenserver.small2.ip>
    hypervisor:
      host: XEN-TEST
  worker-2:
    role: worker
    address: $<profile:xenserver.small3.ip>
    hypervisor:
      host: XEN-TEST
";
                return Preprocess(clusterDefinition);
            }
        }

        /// <summary>
        /// Cluster definition that deploys a cluster with 3 control-plane nodes and 3 workers on XenServer.
        /// </summary>
        public static string Large
        {
            get
            {
                const string clusterDefinition = @"
name: test-xenserver-large
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: false
hosting:
  environment: xenserver
  hypervisor:
    hostUsername: $<secret:XENSERVER_LOGIN[username]>
    hostPassword: $<secret:XENSERVER_LOGIN[password]>
    namePrefix: test-large
    vcpus: 4
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
    hypervisor:
      host: XEN-TEST
  control-1:
    role: control-plane
    address: $<profile:xenserver.large1.ip>
    hypervisor:
      host: XEN-TEST
  control-2:
    role: control-plane
    address: $<profile:xenserver.large2.ip>
    hypervisor:
      host: XEN-TEST
  worker-0:
    role: worker
    address: $<profile:xenserver.large3.ip>
    hypervisor:
      host: XEN-TEST
  worker-1:
    role: worker
    address: $<profile:xenserver.large4.ip>
    hypervisor:
      host: XEN-TEST
  worker-2:
    role: worker
    address: $<profile:xenserver.large5.ip>
    hypervisor:
      host: XEN-TEST
";
                return Preprocess(clusterDefinition);
            }
        }
    }
}
