// -----------------------------------------------------------------------------
// FILE:        AwsClusterDefinitions.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
    /// <b>MAINTAINERS ONLY:</b> <b>AWS</b> cluster definitions used by maintainers for
    /// unit test clusters deployed by <see cref="ClusterFixture"/> on <b>AWS</b>.
    /// </summary>
    public static class AwsClusterDefinitions
    {
        private static string Preprocess(string input)
        {
            using (var reader = new PreprocessReader(new StringReader(input)))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Cluster definition that deployes a single node on AWS.
        /// </summary>
        public static string Tiny
        {
            get
            {
                const string clusterDefinition = @"
name: test-aws-tiny
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: aws
  aws:
    accessKeyId: $<secret:AWS_NEONFORGE[ACCESS_KEY_ID]>
    secretAccessKey: $<secret:AWS_NEONFORGE[SECRET_ACCESS_KEY]>
    availabilityZone: us-west-2a
    defaultEbsOptimized: true
nodes:
   control-0:
     role: control-plane
";
                return Preprocess(clusterDefinition);
            }
        }

        /// <summary>
        /// Cluster definition that deploys a cluster with 1 control-plane and 3 workers on AWS.
        /// </summary>
        public static string Small
        {
            get
            {
                const string clusterDefinition = @"
name: test-aws-small
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: aws
  aws:
    accessKeyId: $<secret:AWS_NEONFORGE[ACCESS_KEY_ID]>
    secretAccessKey: $<secret:AWS_NEONFORGE[SECRET_ACCESS_KEY]>
    availabilityZone: us-west-2a
    defaultEbsOptimized: true
nodes:
   control-0:
     role: control-plane
   worker-0:
     role: worker
   worker-1:
     role: worker
   worker-2:
     role: worker
";
                return Preprocess(clusterDefinition);
            }
        }

        /// <summary>
        /// Cluster definition that deploys a cluster with 3 control-plane nodes and 3 workers on AWS.
        /// </summary>
        public static string Large
        {
            get
            {
                const string clusterDefinition = @"
name: test-aws-large
purpose: test
isLocked: false
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnControlPlane: true
hosting:
  environment: aws
  aws:
    accessKeyId: $<secret:AWS_NEONFORGE[ACCESS_KEY_ID]>
    secretAccessKey: $<secret:AWS_NEONFORGE[SECRET_ACCESS_KEY]>
    availabilityZone: us-west-2a
    defaultEbsOptimized: true
nodes:
   control-0:
     role: control-plane
   control-1:
     role: control-plane
   control-2:
     role: control-plane
   worker-0:
     role: worker
   worker-1:
     role: worker
   worker-2:
     role: worker
";
                return Preprocess(clusterDefinition);
            }
        }
    }
}
