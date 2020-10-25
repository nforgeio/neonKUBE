//-----------------------------------------------------------------------------
// FILE:	    IBashCommandFormatter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

using ICSharpCode.SharpZipLib.Zip;

using Renci.SshNet;
using Renci.SshNet.Common;
using System.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Describes a type implementation that can render a nicely formatted Bash command.
    /// </summary>
    public interface IBashCommandFormatter
    {
        /// <summary>
        /// Renders a nicely formatted Bash command.  Note that the string returned may
        /// include multipe lines with continuation characters.
        /// </summary>
        /// <param name="comment">The optional comment to be included in the output.</param>
        /// <returns>The formatted Bash command.</returns>
        string ToBash(string comment = null);
    }
}
