//-----------------------------------------------------------------------------
// FILE:	    IBashCommandFormatter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Hive
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
