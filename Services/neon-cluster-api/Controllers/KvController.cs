//-----------------------------------------------------------------------------
// FILE:	    TestController.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Neon.Common;
using Neon.Kube;
using Neon.Service;
using Neon.Web;
using Neon.Postgres;
using Npgsql;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Runtime.Serialization;

namespace NeonClusterApi
{
    [Route("v1/kv")]
    [ApiController]
    public class KvController : NeonControllerBase
    {
        /// <summary>
        /// 
        /// </summary>
        private Service kubeKv;
        private KubeKV kvClient;
        public KvController(Service kubeKv)
        {
            this.kubeKv = kubeKv;
            this.kvClient = new KubeKV(kubeKv.DbConnectionString, kubeKv.StateTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPut("{key}")]
        public async Task SetAsync([FromRoute] string key, [FromBody] object value)
        {
            await kvClient.SetAsync(key, value);
        }

        /// <summary>
        ///  
        /// </summary>
        /// <returns></returns>
        [HttpGet("{key}")]
        [Produces("application/json")]
        public async Task<ActionResult<dynamic>> GetAsync([FromRoute] string key)
        {
            try
            {
                return await kvClient.GetAsync<dynamic>(key);
            }
            catch (Exception e)
            {
                LogError(e);

                return NotFound();
            }
        }

        /// <summary>
        ///  
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{keyPattern}")]
        public async Task<ActionResult> RemoveAsync([FromRoute] string keyPattern, [FromQuery] bool regex = false)
        {
            await kvClient.RemoveAsync(keyPattern, regex);
            return NoContent();
        }

        /// <summary>
        ///  
        /// </summary>
        /// <returns></returns>
        [HttpGet("")]
        public async Task<ActionResult<Dictionary<string, object>>> ListAsync([FromQuery] string keyPattern)
        {
            return await kvClient.ListAsync<dynamic>(keyPattern);
        }
    }
}
