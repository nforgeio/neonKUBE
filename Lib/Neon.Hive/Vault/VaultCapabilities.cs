//-----------------------------------------------------------------------------
// FILE:	    VaultCapabilies.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Hive
{
    /// <summary>
    /// Enumerates the possible capabilities controlled by a Vault 
    /// access control policy.
    /// </summary>
    [Flags]
    public enum VaultCapabilies
    {
        /// <summary>
        /// <para>
        /// Create a value at a path.
        /// </para>
        /// <note>
        /// At present, few parts of Vault distinguish between create and update, so most
        /// operations require update. Parts of Vault that provide such a distinction, such
        /// as the generic backend, are noted in documentation.
        /// </note>
        /// </summary>
        [EnumMember(Value = "create")]
        Create = 0x0001,

        /// <summary>
        /// Read the value at a path.
        /// </summary>
        [EnumMember(Value = "read")]
        Read = 0x0002,

        /// <summary>
        /// Change the value at a path. In most parts of Vault, this also includes the ability
        /// to create the initial value at the path.
        /// </summary>
        [EnumMember(Value = "update")]
        Update = 0x0004,

        /// <summary>
        /// Delete the value at a path.
        /// </summary>
        [EnumMember(Value = "delete")]
        Delete = 0x0008,

        /// <summary>
        /// List key names at a path. Note that the keys returned by a list operation are 
        /// not filtered by policies. Do not encode sensitive information in key names.
        /// </summary>
        [EnumMember(Value = "list")]
        List = 0x0010,

        /// <summary>
        /// Gain access to paths that are <b>root-protected</b>. This is additive to other
        /// capabilities, so a path that requires sudo access will also require read,
        /// update, etc. as appropriate.
        /// </summary>
        [EnumMember(Value = "sudo")]
        Sudo = 0x0020,

        /// <summary>
        /// No access allowed. This always takes precedence regardless of any other 
        /// defined capabilities, including <see cref="Sudo"/>.
        /// </summary>
        [EnumMember(Value = "deny")]
        Deny = 0x0040
    }
}
