//-----------------------------------------------------------------------------
// FILE:	    ConsoleApi.cs
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

// This code was adapted from: https://github.com/microsoft/terminal/tree/main/samples/ConPTY/MiniTerm/MiniTerm/Native

using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using Process = Neon.WinTTY.Process;

namespace Neon.WinTTY
{
    /// <summary>
    /// <b>P/Invoke</b> signatures for the WIN32 Console API.
    /// </summary>
    internal static class ConsoleApi
    {
        public const int STD_OUTPUT_HANDLE = -11;
        public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        public const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(SafeFileHandle hConsoleHandle, uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(SafeFileHandle handle, out uint mode);

        public delegate bool ConsoleEventDelegate(CtrlTypes ctrlType);

        public enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
    }
}
