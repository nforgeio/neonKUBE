//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient_Get.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
        private const string baseUri = "http://127.0.0.1:888/";

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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
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
