//-----------------------------------------------------------------------------
// FILE:	    CpuArchitecture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Enumerates the possible CPU architectures.
    /// </summary>
    public enum CpuArchitecture
    {
        /// <summary>
        /// Architecture is unknown.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// 32-bit Intel/AMD.
        /// </summary>
        [EnumMember(Value = "x32")]
        x32,

        /// <summary>
        /// 64-bit Intel/AMD.
        /// </summary>
        [EnumMember(Value = "x64")]
        x64,

        /// <summary>
        /// 32-bit ARM.
        /// </summary>
        [EnumMember(Value = "arm32")]
        ARM32,

        /// <summary>
        /// 64-bit ARM.
        /// </summary>
        [EnumMember(Value = "arm64")]
        ARM64
    }
}
