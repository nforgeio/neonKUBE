//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient.HeadUnsafe.cs
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

using Neon.Collections;
using Neon.Common;
using Neon.Net;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_JsonClient
    {
        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync()
        {
            // Ensure that HEAD returning an explict type works.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "HEAD")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    var output = new ReplyDoc()
                    {
                        Value1 = "Hello World!"
                    };

                    response.ContentType = "application/json";

                    await response.WriteAsync(NeonHelper.JsonSerialize(output));
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    await jsonClient.HeadUnsafeAsync(baseUri + "info");
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_NotJson()
        {
            // Ensure that HEAD returning a non-JSON content type returns a NULL document.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "HEAD")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    var output = new ReplyDoc()
                    {
                        Value1 = "Hello World!"
                    };

                    response.ContentType = "application/not-json";

                    await response.WriteAsync(NeonHelper.JsonSerialize(output));
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    await jsonClient.HeadUnsafeAsync(baseUri + "info");
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_Args()
        {
            // Ensure that HEAD with query arguments work.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "HEAD")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    var output = new ReplyDoc()
                    {
                        Value1 = request.QueryGet("arg1"),
                        Value2 = request.QueryGet("arg2")
                    };

                    response.ContentType = "application/json";

                    await response.WriteAsync(NeonHelper.JsonSerialize(output));
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    var args = new ArgDictionary()
                    {
                        { "arg1", "test1" },
                        { "arg2", "test2" }
                    };

                    await jsonClient.HeadUnsafeAsync(baseUri + "info", args: args);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_Headers()
        {
            // Ensure that HEAD with query arguments work.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "HEAD")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    var output = new ReplyDoc()
                    {
                        Value1 = request.Headers["arg1"],
                        Value2 = request.Headers["arg2"]
                    };

                    response.ContentType = "application/json";

                    await response.WriteAsync(NeonHelper.JsonSerialize(output));
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    var headers = new ArgDictionary()
                    {
                        { "arg1", "test1" },
                        { "arg2", "test2" }
                    };

                    await jsonClient.HeadUnsafeAsync(baseUri + "info", headers: headers);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_Dynamic()
        {
            // Ensure that HEAD returning a dynamic works.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "HEAD")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return ;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    var output = new ReplyDoc()
                    {
                        Value1 = "Hello World!"
                    };

                    response.ContentType = "application/json";

                    await response.WriteAsync(NeonHelper.JsonSerialize(output));
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    await jsonClient.HeadUnsafeAsync(baseUri + "info");
                }
            };
        }
 
        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_Dynamic_NotJson()
        {
            // Ensure that HEAD returning non-JSON returns a NULL dynamic document.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "HEAD")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    var output = new ReplyDoc()
                    {
                        Value1 = "Hello World!"
                    };

                    response.ContentType = "application/not-json";

                    await response.WriteAsync(NeonHelper.JsonSerialize(output));
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    await jsonClient.HeadUnsafeAsync(baseUri + "info");
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_Error()
        {
            // Ensure that HEAD returning a hard error works.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var response = context.Response;

                    response.StatusCode = (int)HttpStatusCode.NotFound;

                    await Task.CompletedTask;
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    await jsonClient.HeadUnsafeAsync(baseUri + "info");
                }
            };
        }

        [Fact(Skip = "TODO")]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_Retry()
        {
            // Ensure that HEAD will retry after soft errors.

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }

        [Fact(Skip = "TODO")]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_NoRetryNull()
        {
            // Ensure that HEAD won't retry if [retryPolicy=NULL]

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }

        [Fact(Skip = "TODO")]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task HeadUnsafeAsync_NoRetryExplicit()
        {
            // Ensure that HEAD won't retry if [retryPolicy=NoRetryPolicy]

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }
    }
}
