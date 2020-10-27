//-----------------------------------------------------------------------------
// FILE:	    XenResponse.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace Neon.XenServer
{
    /// <summary>
    /// Holds the response from a XenServer command invoked using the <b>xe</b> CLI.
    /// </summary>
    public class XenResponse
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="response">The low-level SSH command response.</param>
        internal XenResponse(CommandResponse response)
        {
            this.Response = response;
            this.Items    = new List<Dictionary<string, string>>();

            // We need to parse the [xe] results from the standard output.  This will 
            // look something like:

            //uuid ( RO)                : c73af9a3-8f67-0c52-6c8e-fe3208aae490
            //          name-label ( RW): Removable storage
            //    name-description ( RW):
            //                host ( RO): xentest
            //                type ( RO): udev
            //        content-type ( RO): disk
            //
            //
            //uuid ( RO)                : 7a490025-7911-95f8-6058-2d4647d5f855
            //          name-label ( RW): DVD drives
            //    name-description ( RW): Physical DVD drives
            //                host ( RO): xentest
            //                type ( RO): udev
            //        content-type ( RO): iso
            //
            //
            //uuid ( RO)                : 1aedccc5-8b18-4fc8-b498-e776a5ae2702
            //          name-label ( RW): Local storage
            //    name-description ( RW):
            //                host ( RO): xentest
            //                type ( RO): lvm
            //        content-type ( RO): user
            //
            //
            //uuid ( RO)                : e24cd80a-f54a-4c5b-18e8-245c37b5b7e6
            //          name-label ( RW): XenServer Tools
            //    name-description ( RW): XenServer Tools ISOs
            //                host ( RO): xentest
            //                type ( RO): iso
            //        content-type ( RO): iso
            //
            //

            //
            // This appears to be a list of records with each record being terminated by 
            // two blank lines.  Each line includes a read/write or read-only indicator 
            // (which we'll strip out) followed by a colon and the property value.  I'm
            // not entirely sure if this fully describes the format so I'm going to be
            // a bit defensive below.

            using (var reader = new StringReader(response.OutputText))
            {
                var isEOF = false;

                while (!isEOF)
                {
                    // Read the next record.

                    var rawRecord = new Dictionary<string, string>();

                    while (true)
                    {
                        var line = reader.ReadLine();

                        if (line == null)
                        {
                            isEOF = true;
                            break;
                        }

                        line = line.Trim();

                        if (line == string.Empty)
                        {
                            // Looks like the end of the record, so read the
                            // second blank line.

                            line = reader.ReadLine();
                            Covenant.Assert(line.Trim() == string.Empty);

                            if (line == null)
                            {
                                isEOF = true;
                                break;
                            }
                            else if (line != string.Empty)
                            {
                                Covenant.Assert(line.Trim() == string.Empty, "Unexpected XenServer [xe] CLI output.");
                            }

                            break;
                        }

                        // Parse the property name and value.

                        var colonPos = line.IndexOf(':');

                        if (colonPos == -1)
                        {
                            continue;   // We shouldn't ever see this.
                        }

                        var namePart  = line.Substring(0, colonPos).Trim();
                        var valuePart = line.Substring(colonPos + 1).Trim();

                        var parenPos  = namePart.IndexOf('(');

                        if (parenPos != -1)
                        {
                            namePart = namePart.Substring(0, parenPos).Trim();
                        }

                        rawRecord.Add(namePart, valuePart);
                    }

                    // Add the record to the results if it's not empty.

                    if (rawRecord.Count > 0)
                    {
                        Items.Add(rawRecord);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the low-level command response.
        /// </summary>
        public CommandResponse Response { get; private set; }

        /// <summary>
        /// Returns the command exit code.
        /// </summary>
        public int ExitCode => Response.ExitCode;

        /// <summary>
        /// Ensures that the command executed successfully.
        /// </summary>
        /// <exception cref="ExecuteException">Thrown when the command failed.</exception>
        public void EnsureSuccess() => Response.EnsureSuccess();

        /// <summary>
        /// The list of raw property dictionaries returned by the command.
        /// </summary>
        public List<Dictionary<string, string>> Items { get; private set; }
    }
}
