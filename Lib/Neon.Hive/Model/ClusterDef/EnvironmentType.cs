//-----------------------------------------------------------------------------
// FILE:	    EnvironmentType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Runtime.Serialization;

namespace Neon.Hive
{
    /// <summary>
    /// Enumerates the types of hive operating environments.
    /// </summary>
    public enum EnvironmentType
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        [EnumMember(Value = "other")]
        Other = 0,

        /// <summary>
        /// Development environment.
        /// </summary>
        [EnumMember(Value = "development")]
        Development,

        /// <summary>
        /// Test environment.
        /// </summary>
        [EnumMember(Value = "test")]
        Test,

        /// <summary>
        /// Staging environment.
        /// </summary>
        [EnumMember(Value = "staging")]
        Staging,

        /// <summary>
        /// Production environment.
        /// </summary>
        [EnumMember(Value = "production")]
        Production
    }
}
