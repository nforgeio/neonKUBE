//-----------------------------------------------------------------------------
// FILE:	    AnsibleVerbosity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli.Ansible
{
    /// <summary>
    /// Enumerates the Ansible module output verbosity levels.
    /// </summary>
    public enum AnsibleVerbosity : int
    {
        /// <summary>
        /// Always writes output.
        /// </summary>
        Important = 0,

        /// <summary>
        /// Writes information messages.
        /// </summary>
        Info = 1,

        /// <summary>
        /// Writes trace messages.
        /// </summary>
        Trace = 2
    }

}
