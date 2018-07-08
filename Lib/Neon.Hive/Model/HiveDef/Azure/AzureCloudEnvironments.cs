//-----------------------------------------------------------------------------
// FILE:	    AzureCloudEnvironments.cs
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
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Enumerates the possible Azure hosting environments.
    /// </summary>
    public enum AzureCloudEnvironments
    {
        /// <summary>
        /// Public Azure cloud (default).
        /// </summary>
        [EnumMember(Value = "global-cloud")]
        GlobalCloud = 0,

        /// <summary>
        /// Custom cloud where the management URIs
        /// will be specified explicitly.
        /// </summary>
        [EnumMember(Value = "custom")]
        Custom,

        /// <summary>
        /// China cloud.
        /// </summary>
        [EnumMember(Value = "china")]
        ChinaCloud,

        /// <summary>
        /// German cloud.
        /// </summary>
        [EnumMember(Value = "german")]
        GermanCloud,

        /// <summary>
        /// United States Government cloud.
        /// </summary>
        [EnumMember(Value = "us-government")]
        USGovernment
    }
}
