//-----------------------------------------------------------------------------
// FILE:	    ConsulInfo.cs
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

namespace Neon.Hive
{
    /// <summary>
    /// Used to parse the output of the <b>consul info</b> command into an
    /// easy to use form.
    /// </summary>
    public class ConsulInfo : Dictionary<string, string>
    {
        /// <summary>
        /// Parses the <b>consul info</b> command output such that indvidual
        /// values can be queried using the <b>key</b>.<b>item</b> syntax.
        /// </summary>
        /// <param name="commandOutput">The Conul command output.</param>
        /// <remarks>
        /// <para>
        /// The <b>consul info</b> command returns output that looks something
        /// list this: https://www.consul.io/docs/commands/info.html.
        /// </para>
        /// <para>
        /// As you can see, it includes top-level keys such as <b>agent</b>
        /// and <b>consul</b> and then indvidual data items below each key.
        /// This class parses the output by prepending the top-level key
        /// and a period (<b>.</b>) to each data item, extracting the value
        /// to the right of the equals (<b>=</b>) sign and adding the pair
        /// to the base dictionary.
        /// </para>
        /// <para>
        /// So for example, code that wishes to determine of the command
        /// reports that the node is the Consul leader, we'd combine the
        /// <b>"raft"</b> key with the <b>"state"</b> item and look for
        /// the value being set as <b>"Leader"</b>:
        /// </para>
        /// <code language="c#">
        /// var consulInfo = new ConsulInfo("[command output]");
        /// var isLeader   = consulInfo["raft.state"] == "Leader";
        /// </code>
        /// </remarks>
        public ConsulInfo(string commandOutput)
        {
            using (var reader = new StringReader(commandOutput))
            {
                var key = string.Empty;

                foreach (var line in reader.Lines())
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (line.EndsWith(":"))
                    {
                        key = line.Substring(0, line.Length - 1).Trim();
                        continue;
                    }

                    var posEquals = line.IndexOf('=');

                    if (posEquals < 0)
                    {
                        continue;
                    }

                    var item  = line.Substring(0, posEquals).Trim();
                    var value = line.Substring(posEquals + 1).Trim();

                    base.Add($"{key}.{item}", value);
                }
            }
        }
    }
}
