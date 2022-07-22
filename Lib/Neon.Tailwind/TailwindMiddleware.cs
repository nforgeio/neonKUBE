//-----------------------------------------------------------------------------
// FILE:	    TailwindMiddleware.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Neon.Common;
using Neon.Tailwind;

namespace Neon.Tailwind
{
    internal class NodeRunner : IDisposable
    {
        private Process process;

        [UnsupportedOSPlatform("browser")]
        public NodeRunner(string executable, string[] args, CancellationToken cancellationToken = default)
        {
            
            var processStartInfo = new ProcessStartInfo(executable)
            {
                Arguments = string.Join(' ', args),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process = Process.Start(processStartInfo);
            process.EnableRaisingEvents = true;

            cancellationToken.Register(((IDisposable)this).Dispose);
        }

        [UnsupportedOSPlatform("browser")]
        void IDisposable.Dispose()
        {
            if (process != null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process = null;
            }
        }
    }
}
