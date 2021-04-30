//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient_Get.cs
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Net;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_JsonClient
    {
        private readonly string baseUri = $"http://127.0.0.1:{NetHelper.GetUnusedIpPort(IPAddress.Loopback)}/";

        public class RequestDoc
        {
            public string Operation { get; set; }
            public string Arg0 { get; set; }
            public string Arg1 { get; set; }
        }

        public class ReplyDoc
        {
            public string Value1 { get; set; }
            public string Value2 { get; set; }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void Defaults()
        {
            using (var jsonClient = new JsonClient())
            {
                Assert.IsType<ExponentialRetryPolicy>(jsonClient.SafeRetryPolicy);
                Assert.IsType<NoRetryPolicy>(jsonClient.UnsafeRetryPolicy);
            }
        }
    }
}
