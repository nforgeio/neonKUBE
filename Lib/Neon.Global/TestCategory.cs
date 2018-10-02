//-----------------------------------------------------------------------------
// FILE:	    TestCategory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Xunit
{
    /// <summary>
    /// Defines constants used to help categorize unit tests and avoid
    /// spelling errors and inconsistencies.
    /// </summary>
    internal static class TestCategory
    {
        /// <summary>
        /// Identifies the trait.
        /// </summary>
        public const string CategoryTrait = "Category";

        /// <summary>
        /// Identifies sample tests.
        /// </summary>
        public const string Sample = "Sample";

        /// <summary>
        /// Identifies <b>Neon.Common</b> tests.
        /// </summary>
        public const string NeonCommon = "Neon.Common";

        /// <summary>
        /// Identifies <b>Neon.Hive</b> tests.
        /// </summary>
        public const string NeonHive = "Neon.Hive";

        /// <summary>
        /// Identifies <b>Neon.Couchbase</b> tests.
        /// </summary>
        public const string NeonCouchbase = "Neon.Couchbase";

        /// <summary>
        /// Identifies <b>Neon.HiveMQ</b> tests.
        /// </summary>
        public const string NeonHiveMQ = "Neon.HiveMQ";

        /// <summary>
        /// Identifies <b>neon-cli</b> tests.
        /// </summary>
        public const string NeonCli = "neon-cli";
    }
}
