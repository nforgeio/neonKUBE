//-----------------------------------------------------------------------------
// FILE:	    SshProxyException.cs
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
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.SSH
{
    /// <summary>
    /// Thrown for <see cref="LinuxSshProxy{TMetadata}"/> errors.
    /// </summary>
    public class SshProxyException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public SshProxyException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
