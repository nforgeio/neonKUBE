//-----------------------------------------------------------------------------
// FILE:	    TestArea.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Xunit
{
    /// <summary>
    /// Defines the neonFORGE related test areas.  These currently map to 
    /// neonFORGE related projects.  Use these in <c>[Trait(TestTrait.Category, ...)]</c>
    /// attributes tagging your test methods.
    /// </summary>
    public static class TestArea
    {
        /// <summary>
        /// Identifies <b>Neon.Cadence</b> tests.
        /// </summary>
        public const string NeonCadence = "Neon.Cadence";

        /// <summary>
        /// Identifies <b>Neon.Temporal</b> tests.
        /// </summary>
        public const string NeonTemporal = "Neon.Temporal";

        /// <summary>
        /// Identifies <b>Neon.ModelGen</b> tests.
        /// </summary>
        public const string NeonModelGen = "Neon.ModelGen";

        /// <summary>
        /// Identifies <b>Neon.Common</b> tests.
        /// </summary>
        public const string NeonCommon = "Neon.Common";

        /// <summary>
        /// Identifies <b>Neon.Cryptography</b> tests.
        /// </summary>
        public const string NeonCryptography = "Neon.Cryptography";

        /// <summary>
        /// Identifies <b>Neon.Deployment</b> tests.
        /// </summary>
        public const string NeonDeployment = "Neon.Deployment";

        /// <summary>
        /// Identifies <b>Neon.Kube</b> tests.
        /// </summary>
        public const string NeonKube = "Neon.Kube";

        /// <summary>
        /// Identifies <b>Neon.Cloud</b> tests.
        /// </summary>
        public const string NeonCloud = "Neon.Cloud";

        /// <summary>
        /// Identifies <b>Neon.Couchbase</b> tests.
        /// </summary>
        public const string NeonCouchbase = "Neon.Couchbase";

        /// <summary>
        /// Identifies <b>neon-cli</b> tests.
        /// </summary>
        public const string NeonCli = "neon-cli";

        /// <summary>
        /// Identifies <b>neon-desktop</b> tests.
        /// </summary>
        public const string NeonDesktop = "neon-desktop";

        /// <summary>
        /// Identifies the <b>Neon.Service</b> tests.
        /// </summary>
        public const string NeonService = "Neon.Service";

        /// <summary>
        /// Identifies <b>neon-xunit</b> tests.
        /// </summary>
        public const string NeonXunit = "neon-xunit";

        /// <summary>
        /// Identifies <b>Neon.Web</b> tests.
        /// </summary>
        public const string NeonWeb = "Neon.Web";

        /// <summary>
        /// Identifies <b>Neon.YugaByte</b> tests.
        /// </summary>
        public const string NeonYugaByte = "Neon.YugaByte";

        /// <summary>
        /// Identifies <b>Neon.Postgres</b> tests.
        /// </summary>
        public const string NeonPostgres = "Neon.Postgres";

        /// <summary>
        /// Identifies <b>Neon.Cassandra</b> tests.
        /// </summary>
        public const string NeonCassandra = "Neon.Cassandra";

        /// <summary>
        /// Identifies <b>Neon.WSL</b> tests.
        /// </summary>
        public const string NeonWSL = "Neon.WSL";
    }
}
