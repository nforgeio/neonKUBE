//-----------------------------------------------------------------------------
// FILE:	    PseudoConsole.cs
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

// This code was adapted from: https://github.com/microsoft/terminal/tree/main/samples/ConPTY/MiniTerm/MiniTerm/Native

using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using static Neon.WinTTY.PseudoConsoleApi;

namespace Neon.WinTTY
{
    /// <summary>
    /// A pipe used to talk to the pseudoconsole, as described in:
    /// https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session
    /// </summary>
    /// <remarks>
    /// We'll have two instances of this class, one for input and one for output.
    /// </remarks>
    internal sealed class PseudoConsolePipe : IDisposable
    {
        public readonly SafeFileHandle ReadSide;
        public readonly SafeFileHandle WriteSide;

        public PseudoConsolePipe()
        {
            if (!CreatePipe(out ReadSide, out WriteSide, IntPtr.Zero, 0))
            {
                throw new InvalidOperationException("failed to create pipe");
            }
        }

        public void Dispose()
        {
            ReadSide?.Dispose();
            WriteSide?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
