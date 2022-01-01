//-----------------------------------------------------------------------------
// FILE:	    TestController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Neon.Common;
using Neon.Service;
using Neon.Web;

namespace TestApiService
{
    [Route("/")]
    [ApiController]
    public class TestController : NeonControllerBase
    {
        /// <summary>
        /// Echos the query string, request contents, or <b>"HELLO WORLD!"</b> back
        /// to the caller.
        /// </summary>
        /// <returns>The echo string.</returns>
        [HttpGet("echo")]
        public async Task EchoAsync()
        {
            // Return the query string if there is one.

            if (Request.QueryString.HasValue)
            {
                // Strip the leading "?" from the string.

                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Request.QueryString.Value[1..]));
                return;
            }

            // Echo any uploaded content.

            var cbContent = 0L;

            if (Request.Body != null)
            {
                var buffer = new byte[1024];

                while (true)
                {
                    var cb = await Request.Body.ReadAsync(buffer, 0, buffer.Length);

                    if (cb == 0)
                    {
                        break;
                    }

                    await Response.Body.WriteAsync(buffer, 0, cb);
                    cbContent += cb;
                }

                if (cbContent > 0)
                {
                    return;
                }
            }

            // Otherwise, return this:

            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("HELLO WORLD!"));
        }

        /// <summary>
        /// Signals the service to terminate.
        /// </summary>
        [HttpPost("exit")]
        public async Task ExitAsync()
        {
            // We're going to start a parallel task that will wait a bit so the
            // HTTP reply can be transmitted before terminating the service.

            _ = Task.Run(
                    async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        Environment.Exit(0);
                    });

            await Task.CompletedTask;
        }
    }
}
