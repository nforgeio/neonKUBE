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
        ///     <item><c>AWS_ACCESS_KEY_ID</c></item>
        ///     <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        /// <param name="awsAccessKeyId">Optionally specfies a custom name for the AWS <b>access key ID</b> secret.</param>
        /// <param name="awsSecretAccessKey">Optionally specfies a custom name for the AWS <b>access key</b> secret.</param>
        public void GetAwsCredentials(string awsAccessKeyId = "AWS_ACCESS_KEY_ID", string awsSecretAccessKey = "AWS_SECRET_ACCESS_KEY")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(awsAccessKeyId), nameof(awsAccessKeyId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(awsSecretAccessKey), nameof(awsSecretAccessKey));

            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", GetSecretPassword(awsAccessKeyId));
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", GetSecretPassword(awsSecretAccessKey));
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
