//-----------------------------------------------------------------------------
// FILE:        HyperVClusterDefinitions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
    /// <b>MAINTAINER ONLY:</b> <b>Hyper-V</b> cluster definitions used by maintainers for
    /// unit test clusters deployed by <see cref="ClusterFixture"/> on <b>Hyper-V</b>.
    /// </summary>
    public static class HyperVClusterDefinitions
    {
        private static string Preprocess(string input)
        {
            using (var reader = new PreprocessReader(new StringReader(input)))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Cluster definition that deployes a single node on Hyper-V.
        /// </summary>
        public static string Tiny
        {
            get
            {
                const string clusterDefinition = @"
name: test-hyperv-tiny
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: hyperv
  hypervisor:
    namePrefix: test-tiny
    vcpus: 4
    memory: 16 GiB
    bootDisk: 64 GiB
    mayastorDisk: 10 GiB
    diskLocation: $<profile:hyperv.diskfolder>
network:
  premiseSubnet: $<profile:lan.subnet>
  gateway: $<profile:lan.gateway>
  nameservers:
  - $<profile:lan.dns0>
  - $<profile:lan.dns1>
nodes:
  control-0:
    role: control-plane
    address: $<profile:hyperv.tiny0.ip>
";
                return Preprocess(clusterDefinition);
            }
        }

        /// <summary>
        /// Cluster definition that deploys a cluster with 1 control-plane and 3 workers on Hyper-V.
        /// </summary>
        public static string Small
        {
            get
            {
                const string clusterDefinition = @"
name: test-hyperv-small
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: false
hosting:
  environment: hyperv
  hypervisor:
    namePrefix: test-small
    vcpus: 4
    memory: 8 GiB
    bootDisk: 64 GiB
    mayastorDisk: 10 GiB
    diskLocation: $<profile:hyperv.diskfolder>
network:
  premiseSubnet: $<profile:lan.subnet>
  gateway: $<profile:lan.gateway>
  nameservers:
  - $<profile:lan.dns0>
  - $<profile:lan.dns1>
nodes:
  control-0:
    role: control-plane
    address: $<profile:hyperv.small0.ip>
  worker-0:
    role: worker
    address: $<profile:hyperv.small1.ip>
  worker-1:
    role: worker
    address: $<profile:hyperv.small2.ip>
  worker-2:
    role: worker
    address: $<profile:hyperv.small3.ip>
";
                return Preprocess(clusterDefinition);
            }
        }

        /// <summary>
        /// Cluster definition that deploys a cluster with 3 control-plane nodes and 3 workers on Hyper-V.
        /// </summary>
        public static string Large
        {
            get
            {
                const string clusterDefinition = @"
name: test-hyperv-large
datacenter: $<profile:datacenter>
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: false
hosting:
  environment: hyperv
  hypervisor:
    namePrefix: test-large
    vcpus: 4
    memory: 8 GiB
    bootDisk: 64 GiB
    mayastorDisk: 10 GiB
    diskLocation: $<profile:hyperv.diskfolder>
network:
  premiseSubnet: $<profile:lan.subnet>
  gateway: $<profile:lan.gateway>
  nameservers:
  - $<profile:lan.dns0>
  - $<profile:lan.dns1>
nodes:
  control-0:
    role: control-plane
    address: $<profile:hyperv.large0.ip>
  control-1:
    role: control-plane
    address: $<profile:hyperv.large1.ip>
  control-2:
    role: control-plane
    address: $<profile:hyperv.large2.ip>
  worker-0:
    role: worker
    address: $<profile:hyperv.large3.ip>
  worker-1:
    role: worker
    address: $<profile:hyperv.large4.ip>
  worker-2:
    role: worker
    address: $<profile:hyperv.large5.ip>
";
                return Preprocess(clusterDefinition);
            }
        }
    }
}
