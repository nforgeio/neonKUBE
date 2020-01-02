//-----------------------------------------------------------------------------
// FILE:	    Test_ValidateController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Couchbase;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

using Test.Neon.Models;

namespace TestModelGen.Validator
{
    /// <summary>
    /// This class verifies that <see cref="XunitExtensions.ValidateController{TServiceController}(Neon.Data.IGeneratedServiceClient)"/>
    /// actually works properly.
    /// </summary>
    public class Test_ValidateController
    {
        //---------------------------------------------------------------------

        [Route("")]
        public class VerifyController0_Match
        {
        }

        [Route("/xxx")]
        public class VerifyController0_DifferentRoute
        {
        }

        [Route("")]
        public class VerifyController0_ExtraMethod
        {
            public void Hello()
            {
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public void ValidateController0()
        {
            using (var client = new VerifyController0Client())
            {
                // These one should pass.

                client.ValidateController<VerifyController0_Match>();

                // These should all fail.

                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController0_DifferentRoute>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController0_ExtraMethod>());
            }
        }

        //---------------------------------------------------------------------

        [Route("/foo")]
        public class VerifyController1_Match
        {
            public void Hello()
            {
            }
        }

        [Route("/foo")]
        public class VerifyController1_Async
        {
            public async Task Hello()
            {
                await Task.CompletedTask;
            }
        }

        [Route("/foo")]
        public class VerifyController1_ActionResult : ControllerBase
        {
            public ActionResult Hello()
            {
                return Ok();
            }
        }

        [Route("/foo")]
        public class VerifyController1_DifferentRoute
        {
            [Route("DIFFERENT")]
            public void Hello()
            {
            }
        }

        [Route("/foo")]
        public class VerifyController1_MissingMethod
        {
        }

        [Route("/foo")]
        public class VerifyController1_ExtraMethod
        {
            public void Hello()
            {
            }

            public void ExtraMethod()
            {
            }
        }

        [Route("/foo")]
        public class VerifyController1_ExtraParam
        {
            public void Hello(string arg)
            {
            }
        }

        [Route("/foo")]
        public class VerifyController1_DifferentResult
        {
            public string Hello(string arg)
            {
                return "Hello World!";
            }
        }

        [Route("/")]
        public class VerifyController1_DifferentHttpMethod
        {
            [HttpPut]
            public void Hello()
            {
            }
        }

        [Route("/foo")]
        public class VerifyController1_DifferentParamName
        {
            public void Hello(string DIFFERENT)
            {
            }
        }

        [Route("/foo")]
        public class VerifyController1_DifferentParamType
        {
            public void Hello(int arg)
            {
            }
        }

        [Route("/foo")]
        public class VerifyController1_DifferentParamMarshalling
        {
            public void Hello([Microsoft.AspNetCore.Mvc.FromBody]string arg)
            {
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonModelGen)]
        public void ValidateController1()
        {
            using (var client = new VerifyController1Client())
            {
                // These all should pass.

                client.ValidateController<VerifyController1_Match>();
                client.ValidateController<VerifyController1_Async>();
                client.ValidateController<VerifyController1_ActionResult>();

                // These should all fail.

                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_DifferentRoute>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_ExtraMethod>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_MissingMethod>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_ExtraParam>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_DifferentResult>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_DifferentHttpMethod>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_DifferentParamName>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_DifferentParamType>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<VerifyController1_DifferentParamMarshalling>());
            }
        }
    }
}
