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

using Process = Neon.WinTTY.Process;

namespace Neon.WinTTY
{
    /// <summary>
    /// Implements a pseudo console.
    /// </summary>
    internal sealed class PseudoConsole : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a pseudo console using the specified input/output streams as well as
        /// the console size.
        /// </summary>
        /// <param name="inputReadSide">The input readside stream.</param>
        /// <param name="outputWriteSide">The output write side stream.</param>
        /// <param name="width">Specifies the console width in characters.</param>
        /// <param name="height">Specifies the console heright in characters.</param>
        /// <returns>The new console.</returns>
        internal static PseudoConsole Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, int width, int height)
        {
            Covenant.Requires<ArgumentNullException>(inputReadSide != null, nameof(inputReadSide));
            Covenant.Requires<ArgumentNullException>(outputWriteSide != null, nameof(outputWriteSide));
            Covenant.Requires<ArgumentException>(width > 0, nameof(width));
            Covenant.Requires<ArgumentException>(height > 0, nameof(height));

            var createResult = CreatePseudoConsole(new COORD { X = (short)width, Y = (short)height }, inputReadSide, outputWriteSide, 0, out IntPtr hPC);

            if (createResult != 0)
            {
                throw new InvalidOperationException($"Could not create psuedo console. Error Code: {createResult}");
            }

            return new PseudoConsole(hPC);
        }

        //---------------------------------------------------------------------
        // Instance members

        private bool isDisposed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="handle">The pseudo console handle.</param>
        private PseudoConsole(IntPtr handle)
        {
            Covenant.Requires<ArgumentNullException>(handle != IntPtr.Zero, nameof(handle));

            this.Handle = handle;
        }

        /// <summary>
        /// Returns the thread attribute required for pseudo consoles.
        /// </summary>
        public static readonly IntPtr PseudoConsoleThreadAttribute = (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        /// <summary>
        /// Returns the pseudo console handle.
        /// </summary>
        public IntPtr Handle { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            ClosePseudoConsole(Handle);
        }
    }
}
