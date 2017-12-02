//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient_DeleteUnsafe.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Owin;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Net;
using Neon.Retry;

using Xunit;

namespace TestCommon
{
    public partial class Test_JsonClient
    {
        [Fact]
        public async Task DeleteUnsafeAsync()
        {
            // Ensure that DELETE returning an explict type works.

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "DELETE")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            var output = new ReplyDoc()
                                {
                                    Value1 = "Hello World!"
                                };

                            response.ContentType = "application/json";

                            return response.WriteAsync(NeonHelper.JsonSerialize(output));
                        });
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info")).As<ReplyDoc>();

                    Assert.Equal("Hello World!", reply.Value1);
                }
            };
        }

        [Fact]
        public async Task DeleteUnsafeAsync_NotJson()
        {
            // Ensure that DELETE returning a non-JSON content type returns a NULL document.

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "DELETE")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            var output = new ReplyDoc()
                                {
                                    Value1 = "Hello World!"
                                };

                            response.ContentType = "application/not-json";

                            return response.WriteAsync(NeonHelper.JsonSerialize(output));
                        });
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info")).As<ReplyDoc>();

                    Assert.Null(reply);
                }
            };
        }

        [Fact]
        public async Task DeleteUnsafeAsync_Args()
        {
            // Ensure that DELETE with query arguments work.

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "DELETE")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            var output = new ReplyDoc()
                                {
                                    Value1 = request.Query.Get("arg1"),
                                    Value2 = request.Query.Get("arg2")
                                };

                            response.ContentType = "application/json";

                            return response.WriteAsync(NeonHelper.JsonSerialize(output));
                        });
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info?arg1=test1&arg2=test2")).As<ReplyDoc>();

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);
                }
            };
        }

        [Fact]
        public async Task DeleteUnsafeAsync_Dyanmic()
        {
            // Ensure that DELETE returning a dynamic works.

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "DELETE")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            var output = new ReplyDoc()
                                {
                                    Value1 = "Hello World!"
                                };

                            response.ContentType = "application/json";

                            return response.WriteAsync(NeonHelper.JsonSerialize(output));
                        });
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info")).AsDynamic();

                    Assert.Equal("Hello World!", (string)reply.Value1);
                }
            };
        }
 
        [Fact]
        public async Task DeleteUnsafeAsync_Dynamic_NotJson()
        {
            // Ensure that DELETE returning non-JSON returns a NULL dynamic document.

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "DELETE")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            var output = new ReplyDoc()
                                {
                                    Value1 = "Hello World!"
                                };

                            response.ContentType = "application/not-json";

                            return response.WriteAsync(NeonHelper.JsonSerialize(output));
                        });
                }))
            {
                using (var jsonClient = new JsonClient())
                {
                    var reply = (await jsonClient.DeleteUnsafeAsync(baseUri + "info")).AsDynamic();

                    Assert.Null(reply);
                }
            };
        }

        [Fact]
        public async Task DeleteUnsafeAsync_Error()
        {
            // Ensure that DELETE returning a hard error works.

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var response = context.Response;

                            response.StatusCode = (int)HttpStatusCode.NotFound;

                            return Task.Delay(0);
                        });
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

        [Fact]
        public async Task DeleteUnsafeAsync_Retry()
        {
            // Ensure that DELETE will retry after soft errors.

            // $todo(jeff.lill): Simulate socket errors via HttpClient mocking.

            await Task.Delay(0);
        }

        [Fact]
        public async Task DeleteUnsafeAsync_NoRetryNull()
        {
            // Ensure that DELETE won't retry if [retryPolicy=NULL]

            // $todo(jeff.lill): Simulate socket errors via HttpClient mocking.

            await Task.Delay(0);
        }

        [Fact]
        public async Task DeleteUnsafeAsync_NoRetryExplicit()
        {
            // Ensure that DELETE won't retry if [retryPolicy=NoRetryPolicy]

            // $todo(jeff.lill): Simulate socket errors via HttpClient mocking.

            await Task.Delay(0);
        }
    }
}
