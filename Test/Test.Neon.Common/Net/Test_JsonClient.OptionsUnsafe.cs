//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient.OptionsUnsafe.cs
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
using System.Dynamic;
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
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync()
        {
            // Ensure that OPTIONS returning an explict type works.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "OPTIONS")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(request.GetBodyText());

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    var reply = (await jsonClient.OptionsUnsafeAsync(baseUri + "info", doc)).As<ReplyDoc>();

                    Assert.Equal("Hello World!", reply.Value1);

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_NotJson()
        {
            // Ensure that OPTIONS returning a non-JSON content type returns a NULL document.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "OPTIONS")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(request.GetBodyText());

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    var reply = (await jsonClient.OptionsUnsafeAsync(baseUri + "info", doc)).As<ReplyDoc>();

                    Assert.Null(reply);

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_Args()
        {
            // Ensure that OPTIONS with query arguments work.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "OPTIONS")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(request.GetBodyText());

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

                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    var reply = (await jsonClient.OptionsUnsafeAsync(baseUri + "info", doc, args: args)).As<ReplyDoc>();

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_Headers()
        {
            // Ensure that OPTIONS with query arguments work.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "OPTIONS")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(request.GetBodyText());

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

                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    var reply = (await jsonClient.OptionsUnsafeAsync(baseUri + "info", doc, headers: headers)).As<ReplyDoc>();

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_Dynamic()
        {
            // Ensure that OPTIONS returning a dynamic works.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "OPTIONS")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(request.GetBodyText());

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
                    dynamic doc = new ExpandoObject();

                    doc.Operation = "FOO";
                    doc.Arg0      = "Hello";
                    doc.Arg1      = "World";

                    var reply = (await jsonClient.OptionsUnsafeAsync(baseUri + "info", doc)).AsDynamic();

                    Assert.Equal("Hello World!", (string)reply.Value1);

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);
                }
            };
        }
 
        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_Dynamic_NotJson()
        {
            // Ensure that OPTIONS returning non-JSON returns a NULL dynamic document.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "OPTIONS")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return;
                    }

                    if (request.Path.ToString() != "/info")
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(request.GetBodyText());

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
                    dynamic doc = new ExpandoObject();

                    doc.Operation = "FOO";
                    doc.Arg0 = "Hello";
                    doc.Arg1 = "World";

                    var reply = (await jsonClient.OptionsUnsafeAsync(baseUri + "info", doc)).AsDynamic();

                    Assert.Null(reply);

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);
                }
            };
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_Error()
        {
            // Ensure that OPTIONS returning a hard error works.

            using(new MockHttpServer(baseUri,
                async context =>
                {
                    var response = context.Response;

                    response.StatusCode = (int)HttpStatusCode.NotFound;

                    await Task.CompletedTask;
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    var response = await jsonClient.OptionsUnsafeAsync(baseUri + "info", doc);

                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                    Assert.False(response.IsSuccess);
                    Assert.Throws<HttpException>(() => response.EnsureSuccess());
                }
            };
        }

        [Fact(Skip = "TODO")]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_Retry()
        {
            // Ensure that OPTIONS will retry after soft errors.

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }

        [Fact(Skip = "TODO")]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_NoRetryNull()
        {
            // Ensure that OPTIONS won't retry if [retryPolicy=NULL]

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }

        [Fact(Skip = "TODO")]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task OptionsUnsafeAsync_NoRetryExplicit()
        {
            // Ensure that OPTIONS won't retry if [retryPolicy=NoRetryPolicy]

            // $todo(jefflill): Simulate socket errors via HttpClient mocking.

            await Task.CompletedTask;
        }
    }
}
