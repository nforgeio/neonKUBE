//-----------------------------------------------------------------------------
// FILE:	    Test_Headend.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Service;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;

namespace TestKube
{
    public class Test_Headend
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async void TestGetGitHubZipAsync()
        {
            var headendClient = new HeadendClient();
            var zip = await headendClient.GetHelmChartZipAsync("elasticsearch");
            Assert.IsType<byte[]>(zip);
        }

    }
}
