//-----------------------------------------------------------------------------
// FILE:	    Test_NatsStreamingFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

using Neon.Common;
using Neon.Cryptography;
using Neon.Docker;
using Neon.Kube;
using Neon.IO;
using Neon.Web;
using Neon.Xunit;
using Neon.Xunit.Kube;

using NATS.Client;

using Xunit;

namespace TestXunit
{
    public class Test_NatsStreamingFixture : IClassFixture<NatsFixture>
    {
        private NatsFixture fixture;
        private IConnection client;

        public Test_NatsStreamingFixture(NatsFixture fixture)
        {
            // $todo(jeff.lill): DELETE THIS! (remove the "jeff-latest" tag)

            if (fixture.Start(image: "nkubedev/nats-streaming:jeff-latest") == TestFixtureStatus.AlreadyRunning)
            {
                fixture.Restart();
            }

            this.fixture = fixture;
            this.client = fixture.Client;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public void Test1()
        {
            // Simply verify that the client is connected for now.

            Assert.Equal(ConnState.CONNECTED, client.State);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonXunit)]
        public void Test2()
        {
            // This second test will exercise restarting the service.

            Assert.Equal(ConnState.CONNECTED, client.State);
        }
    }
}
