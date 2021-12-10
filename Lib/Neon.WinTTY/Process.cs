//-----------------------------------------------------------------------------
// FILE:	    Process.cs
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

using static Neon.WinTTY.ProcessApi;
using Process = Neon.WinTTY.Process;

namespace Neon.WinTTY
{
    /// <summary>
    /// Implements a custom process wrapper.
    /// </summary>
    internal class Process : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Start and configure a process. The return value represents the process and should be disposed.
        /// </summary>
        /// <param name="command">The process command line.</param>
        /// <param name="attributes">Pointer to the process attributes.</param>
        /// <param name="hPC">The pseudo console handle.</param>
        public static Process Start(string command, IntPtr attributes, IntPtr hPC)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));
            Covenant.Requires<ArgumentNullException>(attributes != IntPtr.Zero, nameof(attributes));
            Covenant.Requires<ArgumentNullException>(hPC != IntPtr.Zero, nameof(hPC));

            var startupInfo = ConfigureProcessThread(hPC, attributes);
            var processInfo = RunProcess(ref startupInfo, command);

            return new Process(startupInfo, processInfo);
        }

        private static STARTUPINFOEX ConfigureProcessThread(IntPtr hPC, IntPtr attributes)
        {
            Covenant.Requires<ArgumentNullException>(hPC != IntPtr.Zero, nameof(hPC));
            Covenant.Requires<ArgumentNullException>(attributes != IntPtr.Zero, nameof(attributes));

            // This method implements the behavior described here:
            //
            // https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session#preparing-for-creation-of-the-child-process

            var lpSize  = IntPtr.Zero;
            var success = InitializeProcThreadAttributeList(
                lpAttributeList:  IntPtr.Zero,
                dwAttributeCount: 1,
                dwFlags:          0,
                lpSize:           ref lpSize
            );

            if (success || lpSize == IntPtr.Zero) // We're not expecting success here, we just want to get the calculated [lpSize].
            {
                throw new InvalidOperationException("Could not calculate the number of bytes for the attribute list. " + Marshal.GetLastWin32Error());
            }

            var startupInfo = new STARTUPINFOEX();

            startupInfo.StartupInfo.cb  = Marshal.SizeOf<STARTUPINFOEX>();
            startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

            success = InitializeProcThreadAttributeList(
                lpAttributeList:  startupInfo.lpAttributeList,
                dwAttributeCount: 1,
                dwFlags:          0,
                lpSize:           ref lpSize
            );

            if (!success)
            {
                throw new InvalidOperationException("Could not set up attribute list. " + Marshal.GetLastWin32Error());
            }

            success = UpdateProcThreadAttribute(
                lpAttributeList: startupInfo.lpAttributeList,
                dwFlags:         0,
                attribute:       attributes,
                lpValue:         hPC,
                cbSize:          (IntPtr)IntPtr.Size,
                lpPreviousValue: IntPtr.Zero,
                lpReturnSize:    IntPtr.Zero
            );

            if (!success)
            {
                throw new InvalidOperationException("Could not set pseudoconsole thread attribute. " + Marshal.GetLastWin32Error());
            }

            return startupInfo;
        }

        private static PROCESS_INFORMATION RunProcess(ref STARTUPINFOEX sInfoEx, string command)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            int securityAttributeSize = Marshal.SizeOf<SECURITY_ATTRIBUTES>();
            var pSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var tSec = new SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var success = CreateProcess(
                lpApplicationName:    null,
                lpCommandLine:        command,
                lpProcessAttributes:  ref pSec,
                lpThreadAttributes:   ref tSec,
                bInheritHandles:      false,
                dwCreationFlags:      EXTENDED_STARTUPINFO_PRESENT,
                lpEnvironment:        IntPtr.Zero,
                lpCurrentDirectory:   null,
                lpStartupInfo:        ref sInfoEx,
                lpProcessInformation: out PROCESS_INFORMATION pInfo
            );

            if (!success)
            {
                throw new InvalidOperationException("Could not create process. " + Marshal.GetLastWin32Error());
            }

            return pInfo;
        }

        //---------------------------------------------------------------------
        // Instance members

        private bool isDisposed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="startupInfo">The startup information.</param>
        /// <param name="processInfo">The process information.</param>
        internal Process(STARTUPINFOEX startupInfo, PROCESS_INFORMATION processInfo)
        {
            StartupInfo = startupInfo;
            ProcessInfo = processInfo;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~Process()
        {
            Dispose(false);
        }

        /// <summary>
        /// Actually handles disposal.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> when disposing, <c>false</c> when finalizing.</param>
        protected void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            // Free the attribute list.

            if (StartupInfo.lpAttributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(StartupInfo.lpAttributeList);
                Marshal.FreeHGlobal(StartupInfo.lpAttributeList);
            }

            // Close process and thread handles.

            if (ProcessInfo.hProcess != IntPtr.Zero)
            {
                CloseHandle(ProcessInfo.hProcess);
            }

            if (ProcessInfo.hThread != IntPtr.Zero)
            {
                CloseHandle(ProcessInfo.hThread);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns the startup information.
        /// </summary>
        public STARTUPINFOEX StartupInfo { get; private set; }

        /// <summary>
        /// Returns the process information.
        /// </summary>
        public PROCESS_INFORMATION ProcessInfo { get; private set; }
    }
}
