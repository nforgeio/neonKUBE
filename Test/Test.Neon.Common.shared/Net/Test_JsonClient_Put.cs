//-----------------------------------------------------------------------------
// FILE:	    Test_JsonClient_Put.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Dynamic;
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
        public async Task PutAsync()
        {
            // Ensure that PUT sending and returning an explict types works.

            RequestDoc requestDoc = null;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "PUT")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(GetBodyText(request));

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
            };
        }

        [Fact]
        public async Task PutDynamicAsync()
        {
            // Ensure that PUT sending a dynamic document works.

            RequestDoc requestDoc = null;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request = context.Request;
                            var response = context.Response;

                            if (request.Method != "PUT")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(GetBodyText(request));

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
            };
        }

        [Fact]
        public async Task PutAsync_NotJson()
        {
            // Ensure that PUT returning a non-JSON content type returns a NULL document.

            RequestDoc requestDoc = null;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "PUT")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(GetBodyText(request));

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
            };
        }

        [Fact]
        public async Task PutAsync_Args()
        {
            // Ensure that PUT with query arguments work.

            RequestDoc requestDoc = null;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "PUT")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(GetBodyText(request));

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    var reply = (await jsonClient.PutAsync(baseUri + "info?arg1=test1&arg2=test2", doc)).As<ReplyDoc>();

                    Assert.Equal("FOO", requestDoc.Operation);
                    Assert.Equal("Hello", requestDoc.Arg0);
                    Assert.Equal("World", requestDoc.Arg1);

                    Assert.Equal("test1", reply.Value1);
                    Assert.Equal("test2", reply.Value2);
                }
            };
        }

        [Fact]
        public async Task PutAsync_Dyanmic()
        {
            // Ensure that PUT returning a dynamic works.

            RequestDoc requestDoc = null;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "PUT")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(GetBodyText(request));

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
            };
        }
 
        [Fact]
        public async Task PutAsync_Dynamic_NotJson()
        {
            // Ensure that PUT returning non-JSON returns a NULL dynamic document.

            RequestDoc requestDoc = null;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (request.Method != "PUT")
                            {
                                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                return Task.Delay(0);
                            }

                            if (request.Path.ToString() != "/info")
                            {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                return Task.Delay(0);
                            }

                            requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(GetBodyText(request));

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
            };
        }

        [Fact]
        public async Task PutAsync_Error()
        {
            // Ensure that PUT returning a hard error works.

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.PutAsync(baseUri + "info", doc));
                }
            };
        }

        [Fact]
        public async Task PutAsync_Retry()
        {
            // Ensure that PUT will retry after soft errors.

            RequestDoc requestDoc = null;

            var attemptCount = 0;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (attemptCount++ == 0)
                            {
                                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;

                                return Task.Delay(0);
                            }

                            requestDoc = NeonHelper.JsonDeserialize<RequestDoc>(GetBodyText(request));

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
            };
        }

        [Fact]
        public async Task PutAsync_NoRetryNull()
        {
            // Ensure that PUT won't retry if [retryPolicy=NULL]

            var attemptCount = 0;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (attemptCount++ == 0)
                            {
                                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.PutAsync(null, baseUri + "info", doc));

                    Assert.Equal(1, attemptCount);
                }
            };
        }

        [Fact]
        public async Task PutAsync_NoRetryExplicit()
        {
            // Ensure that PUT won't retry if [retryPolicy=NoRetryPolicy]

            var attemptCount = 0;

            using (WebApp.Start(baseUri,
                app =>
                {
                    app.Run(
                        context =>
                        {
                            var request  = context.Request;
                            var response = context.Response;

                            if (attemptCount++ == 0)
                            {
                                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;

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
                    var doc = new RequestDoc()
                    {
                        Operation = "FOO",
                        Arg0      = "Hello",
                        Arg1      = "World"
                    };

                    await Assert.ThrowsAsync<HttpException>(async () => await jsonClient.PutAsync(NoRetryPolicy.Instance, baseUri + "info", doc));

                    Assert.Equal(1, attemptCount);
                }
            };
        }
    }
}
