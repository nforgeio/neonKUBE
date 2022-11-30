//-----------------------------------------------------------------------------
// FILE:	    ChallengeAction.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by NEONFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with NEONFORGE, LLC.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// ACME challenge action.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
    public enum ChallengeAction
    {
        /// <summary>
        /// 
        /// </summary>
        [EnumMember(Value = "Unknown")]
        Unknown,

        /// <summary>
        /// The record will be presented with the solving service.
        /// </summary>
        [EnumMember(Value = "Present")]
        Present,

        /// <summary>
        /// The record will be cleaned up with the solving service.
        /// </summary>
        [EnumMember(Value = "CleanUp")]
        CleanUp
    }
}
