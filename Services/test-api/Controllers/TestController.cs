//-----------------------------------------------------------------------------
// FILE:	    TestController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

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
        public async Task GetEchoAsync()
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
    }
}
