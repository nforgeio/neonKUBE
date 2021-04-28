//-----------------------------------------------------------------------------
// FILE:	    Test_ValidateComposedController.cs
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
    /// actually works properly for clients that compose multiple controllers.
    /// </summary>
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ValidateComposedController
    {
        [Route("/api/v1/user")]
        public class ComposedUserController
        {
            [HttpGet]
            [Route("{id}")]
            public string Get(int id)
            {
                return id.ToString();
            }

            [HttpGet]
            public string[] List()
            {
                return new string[] { "zero", "one", "two" };
            }
        }

        [Route("/api/v1/delivery")]
        public class ComposedDeliveryController
        {
            [HttpGet]
            [Route("{id}")]
            public string Get(int id)
            {
                return id.ToString();
            }

            [HttpGet]
            public string[] List()
            {
                return new string[] { "three", "four", "five" };
            }
        }

        [Route("/api/v1")]
        public class ComposedController
        {
            [HttpGet]
            public string GetVersion()
            {
                return "1.0";
            }
        }

        [Route("/api/v1")]
        public class BadController
        {
            [HttpPost]
            public string BadMethod()
            {
                return string.Empty;
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonModelGen)]
        public void ValidateController0()
        {
            using (var client = new ComposedClient())
            {
                // These one should pass.

                client.User.ValidateController<ComposedUserController>();
                client.Delivery.ValidateController<ComposedDeliveryController>();
                client.ValidateController<ComposedController>();

                // These should all fail.

                Assert.Throws<IncompatibleServiceException>(() => client.User.ValidateController<BadController>());
                Assert.Throws<IncompatibleServiceException>(() => client.Delivery.ValidateController<BadController>());
                Assert.Throws<IncompatibleServiceException>(() => client.ValidateController<BadController>());
            }
        }
    }
}
