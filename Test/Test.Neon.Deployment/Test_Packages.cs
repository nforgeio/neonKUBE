//-----------------------------------------------------------------------------
// FILE:	    Test_Packages.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Xunit;

namespace TestDeployment
{
    [Trait(TestTrait.Category, TestArea.NeonDeployment)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_Packages
    {
        [Fact]
        public void Login()
        {
            // Verify that we can create the client. It will throw an error if there is a 
            // problem logging in.

            var client = new GitHubPackageApi();
        }

        [Fact]
        public async void ListPackages()
        {
            // Verify that we can get the list of packages.

            var client = new GitHubPackageApi();

            var packages = await client.ListAsync("neonkube-dev");

            Assert.NotEmpty(packages);

            packages = await client.ListAsync("neonrelease-dev", "test");

            Assert.NotEmpty(packages);

            packages = await client.ListAsync("neonrelease-dev", "test*");

            Assert.NotEmpty(packages);
        }

        [Fact]
        public async void MakePublic()
        {
            // Verify that we can make a package public.

            var client = new GitHubPackageApi();

            await client.SetVisibilityAsync("neonrelease-dev", "test", GitHubPackageVisibility.Public);

            var packages = await client.ListAsync("neonrelease-dev", "test", visibility: GitHubPackageVisibility.Public);

            Assert.Contains(packages, p => p.Name == "test");
        }

        [Fact]
        public async void MakePrivate()
        {
            // Verify that we can make a package private.

            var client = new GitHubPackageApi();

            await client.SetVisibilityAsync("neonrelease-dev", "test", GitHubPackageVisibility.Private);

            var packages = await client.ListAsync("neonrelease-dev", "test", visibility: GitHubPackageVisibility.Private);

            Assert.Contains(packages, p => p.Name == "test");
        }

        [Fact(Skip = "$todo(marcusbooyah")]
        [Trait(TestTrait.Category, TestTrait.Incomplete)]
        public async void Delete()
        {
            var client = new GitHubPackageApi();

            //await client.DeleteAsync("neonrelease-dev", "test");
        }
    }
}
