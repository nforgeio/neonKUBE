//-----------------------------------------------------------------------------
// FILE:	    SshProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Reflection;
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

using ICSharpCode.SharpZipLib.Zip;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube
{
    /// <summary>
    /// A simplified version of <see cref="SshProxy{TMetadata}"/> that doesn't require
    /// the specification of a metadata type.
    /// </summary>
    public class SshProxy : SshProxy<object>
    {
        /// <summary>
        /// Constructs a <see cref="SshProxy"/>.
        /// </summary>
        /// <param name="name">The display name for the server.</param>
        /// <param name="address">The private cluster IP address for the server.</param>
        /// <param name="credentials">The credentials to be used for establishing SSH connections.</param>
        /// <param name="logWriter">The optional <see cref="TextWriter"/> where operation logs will be written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="name"/> or if <paramref name="credentials"/> is <c>null</c>.
        /// </exception>
        public SshProxy(string name, IPAddress address, SshCredentials credentials, TextWriter logWriter = null)
            : base(name, address, credentials, logWriter)
        {
        }
    }
}
