//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient.DeleteUnsafe.cs
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync()
        {
            // Ensure that DELETE returning an explict type works.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "DELETE")
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
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info")).As<ReplyDoc>();

                    Assert.Equal("Hello World!", reply.Value1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_NotJson()
        {
            // Ensure that DELETE returning a non-JSON content type returns a NULL document.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "DELETE")
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
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info")).As<ReplyDoc>();

                    Assert.Null(reply);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_Args()
        {
            // Ensure that DELETE with query arguments work.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "DELETE")
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

                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info", args: args)).As<ReplyDoc>();

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_Headers()
        {
            // Ensure that DELETE with query arguments work.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "DELETE")
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

                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info", headers: headers)).As<ReplyDoc>();

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_Dynamic()
        {
            // Ensure that DELETE returning a dynamic works.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "DELETE")
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
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info")).AsDynamic();

                    Assert.Equal("Hello World!", (string)reply.Value1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_Dynamic_NotJson()
        {
            // Ensure that DELETE returning non-JSON returns a NULL dynamic document.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "DELETE")
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
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info")).AsDynamic();

                    Assert.Null(reply);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_Error()
        {
            // Ensure that DELETE returning a hard error works.

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
                    var response = await jsonClient.DeleteUnsafeAsync(baseUri + "info");

                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                    Assert.False(response.IsSuccess);
                    Assert.Throws<HttpException>(() => response.EnsureSuccess());
                }
            };
        }

        [Fact(Skip = "TODO")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_Retry()
        {
            // Ensure that DELETE will retry after soft errors.

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }

        [Fact(Skip = "TODO")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_NoRetryNull()
        {
            // Ensure that DELETE won't retry if [retryPolicy=NULL]

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }

        [Fact(Skip = "TODO")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DeleteUnsafeAsync_NoRetryExplicit()
        {
            // Ensure that DELETE won't retry if [retryPolicy=NoRetryPolicy]

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }
    }
}
