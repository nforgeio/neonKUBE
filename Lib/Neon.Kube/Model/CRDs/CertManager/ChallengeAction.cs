//-----------------------------------------------------------------------------
// FILE:	    ChallengeAction.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Neon.Kube
{
    /// <summary>
    /// ACME challenge action.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumMemberConverter))]
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
