//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient.Get.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync()
        {
            // Ensure that GET returning an explict type works.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "GET")
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
                    var reply = (await jsonClient.GetAsync(baseUri + "info")).As<ReplyDoc>();

                    Assert.Equal("Hello World!", reply.Value1);

                    reply = await jsonClient.GetAsync<ReplyDoc>(baseUri + "info");

                    Assert.Equal("Hello World!", reply.Value1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_NotJson()
        {
            // Ensure that GET returning a non-JSON content type returns a NULL document.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "GET")
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
                    var reply = (await jsonClient.GetAsync(baseUri + "info")).As<ReplyDoc>();

                    Assert.Null(reply);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_Args()
        {
            // Ensure that GET with query arguments work.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "GET")
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

                    var reply = (await jsonClient.GetAsync(baseUri + "info", args: args)).As<ReplyDoc>();

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_Headers()
        {
            // Ensure that GET with query arguments work.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "GET")
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

                    var reply = (await jsonClient.GetAsync(baseUri + "info", headers: headers)).As<ReplyDoc>();

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_Dynamic()
        {
            // Ensure that GET returning a dynamic works.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "GET")
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
                    var reply = (await jsonClient.GetAsync(baseUri + "info")).AsDynamic();

                    Assert.Equal("Hello World!", (string)reply.Value1);
                }
            };
        }
 
        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_Dynamic_NotJson()
        {
            // Ensure that GET returning non-JSON returns a NULL dynamic document.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "GET")
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
                    var reply = (await jsonClient.GetAsync(baseUri + "info")).AsDynamic();

                    Assert.Null(reply);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_Error()
        {
            // Ensure that GET returning a hard error works.

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
                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.GetAsync(baseUri + "info"));
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_Retry()
        {
            // Ensure that GET will retry after soft errors.

            var attemptCount = 0;

            using (new MockHttpServer(baseUri,
                 async context =>
                 {
                    var request  = context.Request;
                    var response = context.Response;

                    if (attemptCount++ == 0)
                    {
                        response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;

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
                    var reply = (await jsonClient.GetAsync(baseUri + "info")).AsDynamic();

                    Assert.Equal(2, attemptCount);
                    Assert.Equal("Hello World!", (string)reply.Value1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_NoRetryNull()
        {
            // Ensure that GET won't retry if [retryPolicy=NULL]

            var attemptCount = 0;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (attemptCount++ == 0)
                    {
                        response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;

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
                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.GetAsync(null, baseUri + "info"));

                    Assert.Equal(1, attemptCount);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task GetAsync_NoRetryExplicit()
        {
            // Ensure that GET won't retry if [retryPolicy=NoRetryPolicy]

            var attemptCount = 0;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (attemptCount++ == 0)
                    {
                        response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;

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
                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.GetAsync(NoRetryPolicy.Instance, baseUri + "info"));

                    Assert.Equal(1, attemptCount);
                }
            };
        }
    }
}
