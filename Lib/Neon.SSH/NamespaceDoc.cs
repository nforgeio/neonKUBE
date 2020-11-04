//-----------------------------------------------------------------------------
// FILE:	    NamespaceDoc.cs
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

using Renci.SshNet;

namespace Neon.SSH
{
    /// <summary>
    /// <para>
    /// This namespace includes the <see cref="LinuxSshProxy{TMetadata}"/>, <see cref="LinuxSshProxy"/>
    /// and related classes that wrap and extend the base SSH.NET library clients with additional support for
    /// managing remote Linux machines via SSH including executing commands, scripts, uploading/downloading files, 
    /// and performing idempotent operations.  Remote command executions and their results can also be logged
    /// locally via a <see cref="TextWriter"/> (using a completely non-standard but still useful logging format).
    /// </para>
    /// <para>
    /// The other major type is <see cref="CommandBundle"/>.  Command bundles provide a way to upload a 
    /// script or executable to a temporary working directory and then run the script or program in the 
    /// context of the working directory so the script or program will have access to the files.Command 
    /// bundle executions can also tolerate transient network disconnections.
    /// </para>
    /// <note>
    /// This package has been tested against remote machines running Ubuntu 18.04+ and will probably run
    /// fine on many other Debian-based distributions.  Redhat and other non-Debian distributions probably
    /// won't be compatible.
    /// </note>
    /// </summary>
    public class NamespaceDoc
    {
    }
}
