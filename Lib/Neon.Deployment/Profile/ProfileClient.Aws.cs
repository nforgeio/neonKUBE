//-----------------------------------------------------------------------------
// FILE:	    ProfileClient.Aws.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    public partial class ProfileClient
    {
        /// <summary>
        /// <para>
        /// Retrieves the AWS-CLI NEON_OP_AWS_ACCESS_KEY_ID and NEON_OP_AWS_SECRET_ACCESS_KEY
        /// credentials from 1Password and sets these enviroment variables:
        /// </para>
        /// <list type="bullet">
        ///     <item><c>AWS_ACCESS_KEY_ID</c></item>
        ///     <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        public void GetAwsCredentials()
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", GetSecretPassword("NEON_OP_AWS_ACCESS_KEY_ID"));
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", GetSecretPassword("NEON_OP_AWS_SECRET_ACCESS_KEY"));
        }

        /// <summary>
        /// <para>
        /// Removes the AWS-CLI credential environment variables if present:
        /// </para>
        /// <list type="bullet">
        ///     <item><c>AWS_ACCESS_KEY_ID</c></item>
        ///     <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        public void ClearAwsCredentials()
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
        }
    }
}
