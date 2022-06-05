//-----------------------------------------------------------------------------
// FILE:	    ProfileClient.Aws.cs
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
        /// Retrieves the AWS access key ID and secret access key from 1Password 
        /// and sets these enviroment variables for use by the AWS-CLI:
        /// </para>
        /// <list type="bullet">
        /// <item><c>AWS_ACCESS_KEY_ID</c></item>
        /// <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        /// <param name="secretName">Optionally specifies a custom name for the 1Password secret holding the credentials..</param>
        /// <remarks>
        /// <para>
        /// The AWS credentials are persisted to a 1Password secret for each maintainer, where each user should
        /// be granted individual credentials so they can be easy to revoke if necessary.  We use a single secret
        /// to hold these individual fields:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>ACCESS_KEY_ID</b></term>
        ///     <description>
        ///     Identifies the AWS access key.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>SECRET_ACCESS_KEY</b></term>
        ///     <description>
        ///     The AWS access key secret.
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        public void GetAwsCredentials(string secretName = "AWS_NEONFORGE")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretName), nameof(secretName));

            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", GetSecretValue($"{secretName}[ACCESS_KEY_ID]"));
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", GetSecretValue($"{secretName}[SECRET_ACCESS_KEY]"));
        }

        /// <summary>
        /// <para>
        /// Removes the AWS-CLI credential environment variables if present:
        /// </para>
        /// <list type="bullet">
        /// <item><c>AWS_ACCESS_KEY_ID</c></item>
        /// <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        public void ClearAwsCredentials()
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
        }
    }
}
