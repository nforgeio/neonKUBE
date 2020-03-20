//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient.Put.cs
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
using System.Dynamic;
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
        public async Task PutAsync()
        {
            // Ensure that PUT sending and returning an explict type works.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "PUT")
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

                    var reply = (await jsonClient.PutAsync(baseUri + "info", doc)).As<ReplyDoc>();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal("Hello World!", reply.Value1);

                    reply = await jsonClient.PutAsync<ReplyDoc>(baseUri + "info", doc);

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal("Hello World!", reply.Value1);

                    reply = (await jsonClient.PutAsync(baseUri + "info", @"{""Operation"":""FOO"", ""Arg0"":""Hello"", ""Arg1"":""World""}")).As<ReplyDoc>();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal("Hello World!", reply.Value1);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutDynamicAsync()
        {
            // Ensure that PUT sending a dynamic document works.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request = context.Request;
                    var response = context.Response;

                    if (request.Method != "PUT")
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

                    var reply = (await jsonClient.PutAsync(baseUri + "info", doc)).As<ReplyDoc>();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal("Hello World!", reply.Value1);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_NotJson()
        {
            // Ensure that PUT returning a non-JSON content type returns a NULL document.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "PUT")
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

                    var reply = (await jsonClient.PutAsync(baseUri + "info", doc)).As<ReplyDoc>();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Null(reply);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_Args()
        {
            // Ensure that PUT with query arguments work.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "PUT")
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

                    var reply = (await jsonClient.PutAsync(baseUri + "info", doc, args: args)).As<ReplyDoc>();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_Headers()
        {
            // Ensure that PUT with query arguments work.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "PUT")
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

                    var reply = (await jsonClient.PutAsync(baseUri + "info", doc, headers: headers)).As<ReplyDoc>();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_Dynamic()
        {
            // Ensure that PUT returning a dynamic works.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "PUT")
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

                    var reply = (await jsonClient.PutAsync(baseUri + "info", doc)).AsDynamic();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal("Hello World!", (string)reply.Value1);
                }
            }
        }
 
        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_Dynamic_NotJson()
        {
            // Ensure that PUT returning non-JSON returns a NULL dynamic document.

            RequestDoc requestDoc = null;

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request  = context.Request;
                    var response = context.Response;

                    if (request.Method != "PUT")
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

                    var reply = (await jsonClient.PutAsync(baseUri + "info", doc)).AsDynamic();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Null(reply);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_Error()
        {
            // Ensure that PUT returning a hard error works.

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.PutAsync(baseUri + "info", doc));
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_Retry()
        {
            // Ensure that PUT will retry after soft errors.

            RequestDoc requestDoc = null;

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

                    var reply = (await jsonClient.PutAsync(baseUri + "info", doc)).AsDynamic();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal(2, attemptCount);
                    Assert.Equal("Hello World!", (string)reply.Value1);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_NoRetryNull()
        {
            // Ensure that PUT won't retry if [retryPolicy=NULL]

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.PutAsync(null, baseUri + "info", doc));

                    Assert.Equal(1, attemptCount);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_NoRetryExplicit()
        {
            // Ensure that PUT won't retry if [retryPolicy=NoRetryPolicy]

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.PutAsync(NoRetryPolicy.Instance, baseUri + "info", doc));

                    Assert.Equal(1, attemptCount);
                }
            }
        }

        [PlatformFact(TargetPlatforms.Windows)]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task PutAsync_NullPayloadAsync()
        {
            // Ensure that POST uploading a NULL payload works.

            using (new MockHttpServer(baseUri,
                async context =>
                {
                    var request = context.Request;
                    var response = context.Response;

                    if (request.Method != "PUT")
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
                    var doc = new JsonClientPayload("application/x-www-form-urlencoded", "key1=value1&key2=value2");

                    await jsonClient.PutAsync(baseUri + "info", doc);
                }
            };
        }
    }
}
