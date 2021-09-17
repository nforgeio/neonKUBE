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

namespace NeonKubeKv
{
    [Route("v1/kv")]
    [ApiController]
    public class KvController : NeonControllerBase
    {
        /// <summary>
        /// 
        /// </summary>
        private Service kubeKv;

        public KvController(Service kubeKv)
        {
            this.kubeKv = kubeKv;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPut("{key}")]
        public async Task SetAsync([FromRoute] string key, [FromBody] object value)
        {
            await using var conn = new NpgsqlConnection(kubeKv.DbConnectionString);
            {
                await conn.OpenAsync();
                await using (var cmd = new NpgsqlCommand($@"
    INSERT
        INTO
        {kubeKv.StateTable} (KEY, value)
    VALUES (@k, @v) ON
    CONFLICT (KEY) DO
    UPDATE
    SET
        value = @v", conn))
                {
                    cmd.Parameters.AddWithValue("k", key);
                    cmd.Parameters.AddWithValue("v", value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        ///  
        /// </summary>
        /// <returns></returns>
        [HttpGet("{key}")]
        public async Task<ActionResult<object>> GetAsync([FromRoute] string key)
        {

            await using var conn = new NpgsqlConnection(kubeKv.DbConnectionString);
            {
                await conn.OpenAsync();
                await using (NpgsqlCommand cmd = new NpgsqlCommand($"SELECT value FROM {kubeKv.StateTable} WHERE key='{key}'", conn))
                {
                    return await cmd.ExecuteScalarAsync();
                }
            }
        }

        /// <summary>
        ///  
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{key}")]
        public async Task<ActionResult> RemoveAsync([FromRoute] string key)
        {

            await using var conn = new NpgsqlConnection(kubeKv.DbConnectionString);
            {
                await conn.OpenAsync();
                await using (NpgsqlCommand cmd = new NpgsqlCommand($"DELETE value FROM {kubeKv.StateTable} WHERE key='{key}'", conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                    return NoContent();
                }
            }
        }
    }
}
