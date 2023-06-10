// -----------------------------------------------------------------------------
// FILE:	    Test_ClusterDeployment.cs
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Xunit;
using Neon.XenServer;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Trait(TestTrait.Category, TestTrait.RequiresProfile)]
    [Trait(TestTrait.Category, TestTrait.Slow)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_ClusterDeployment
    {
        private const int repeatCount = 1;

        public Test_ClusterDeployment()
        {
            if (TestHelper.IsClusterTestingEnabled)
            {
                // Register a [ProfileClient] so tests will be able to pick
                // up secrets and profile information from [neon-assistant].

                NeonHelper.ServiceContainer.AddSingleton<IProfileClient>(new MaintainerProfile());
            }
        }
    }
}
