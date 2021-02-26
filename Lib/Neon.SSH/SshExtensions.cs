//-----------------------------------------------------------------------------
// FILE:	    SshExtensions.cs
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.SSH
{
    /// <summary>
    /// Renci SSH.NET related extensions.
    /// </summary>
    public static class SshExtensions
    {
        /// <summary>
        /// Determines whether a file or directory exists on the remote machine.
        /// </summary>
        /// <param name="sftpClient">The FTP client.</param>
        /// <param name="path">Path to the file or directory.</param>
        /// <returns><c>true</c> if the file or directory exists.</returns>
        /// <remarks>
        /// The <see cref="SftpClient.Exists(string)"/> method is supposed to do
        /// this but it appears throw exceptions when part of the path doesn't
        /// exist.  This method calls that but catches and handles the exception.
        /// </remarks>
        public static bool PathExists(this SftpClient sftpClient, string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            try
            {
                return sftpClient.Exists(path);
            }
            catch (SftpPathNotFoundException)
            {
                return false;
            }
        }
    }
}
