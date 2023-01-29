//-----------------------------------------------------------------------------
// FILE:	    Test_Operator.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using k8s;
using k8s.Models;

using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.Operator.Xunit;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Resources.Cluster;

using Xunit;

namespace Test.Neon.Kube.Operator
{
    public class Test_Operator : IClassFixture<TestOperatorFixture>
    {
        private TestOperatorFixture fixture;

        public Test_Operator(TestOperatorFixture fixture) 
        { 
            this.fixture = fixture;
            fixture.Operator.AddController<TestResourceController>();
            fixture.Start();
        }

        [Fact]
        public async Task CreateTestObjectAsync()
        {
            var controller = fixture.Operator.GetController<TestResourceController>();

            var resource = new V1TestResource();
            resource.Spec = new TestSpec()
            {
                Message = "I'm the parent object"
            };

            await controller.ReconcileAsync(resource);

            Assert.Contains(fixture.Resources, r => r.Metadata.Name == "child-object");
        }
    }
}