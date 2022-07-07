//-----------------------------------------------------------------------------
// FILE:	    Program.Replace.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

using Neon.Common;

namespace NeonBuild
{
    public static partial class Program
    {
        /// <summary>
        /// Implements the <b>replace</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        public static void Replace(CommandLine commandLine)
        {
            commandLine = commandLine.Shift(1);

            if (commandLine.Arguments.Length != 3)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var path   = commandLine.Arguments[0];
            var before = commandLine.Arguments[1];
            var after  = commandLine.Arguments[2];
            var text   = File.ReadAllText(path);

            text = text.Replace(before, after);

            File.WriteAllText(path, text);
        }
    }
}
