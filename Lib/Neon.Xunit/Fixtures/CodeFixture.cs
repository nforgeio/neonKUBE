//-----------------------------------------------------------------------------
// FILE:	    CodeFixture.cs
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Retry;
using Neon.Service;

namespace Neon.Xunit
{
    /// <summary>
    /// Used to execute some custom code while <see cref="ComposedFixture"/> is starting
    /// subfixtures.  This is typically used to perform additional configuration of a
    /// <see cref="ServiceMap"/>, etc. to configure components like <see cref="NeonService"/>
    /// instances for integration testing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A common use case for this is to deploy a cluster of services in-process with databases
    /// and/or workflow engines deployed as local Docker containers by other test fixtures.  The
    /// idea is to add a <see cref="CodeFixture"/> via <see cref="ComposedFixture.AddFixture{TFixture}(string, TFixture, Action{TFixture}, int)"/>,
    /// passing the <see cref="Action"/> as a function that performs any custom configuration.
    /// </para>
    /// <note>
    /// <see cref="CodeFixture"/> really doesn't do anything by itself.  It's purpose is simply 
    /// to provide a mechanism for adding and executing your custom code to <see cref="ComposedFixture"/>.
    /// </note>
    /// <para>
    /// You action code can then do things like initialize the database schema and test data
    /// as well as initializing a <see cref="ServiceMap"/> by setting the environment variables
    /// and configuration files for any <see cref="NeonService"/> instances that will also be
    /// deployed for the test.  Many integration test scenarios follow this pattern:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     Implement a unit test derived from <c>IClassFixture&lt;ComposedFixture&gt;</c>.
    ///     </item>
    ///     <item>
    ///     In the test constructor, add any database and/or workflow engine fixtures as
    ///     <b>group=0</b>.  These fixtures will start in parallel and will be running 
    ///     before any fixtures in subsequent groups are started.
    ///     </item>
    ///     <item>
    ///     Then add a <see cref="CodeFixture"/> to the cluster fixture via
    ///     <see cref="ComposedFixture.AddFixture{TFixture}(string, TFixture, Action{TFixture}, int)"/>,
    ///     passing your action as <b>group=1</b>.
    ///     </item>
    ///     <item>
    ///     Your action should perform any custom configuration.
    ///     </item>
    ///     <item>
    ///     Add your <see cref="NeonService"/> and/or other fixtures as <b>group=2</b> or beyond,
    ///     as required.
    ///     </item>
    /// </list>
    /// <para>
    /// So when the <see cref="ComposedFixture"/> starts, it'll start the database/workflow engine
    /// fixtures first as <b>group=0</b> and then start your <see cref="CodeFixture"/> as <b>group=1</b>
    /// and your custom action can initialize the database and perhaps configure a <see cref="ServiceMap"/>.
    /// Once your action has returned, <see cref="ComposedFixture"/> will start the fixtures in any
    /// remaining groups with a configured database and <see cref="ServiceMap"/> before the test framework
    /// starts executing your tests cases.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class CodeFixture : TestFixture
    {
        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public CodeFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~CodeFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !base.IsDisposed)
            {
                Reset();
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Resets the fixture state.
        /// </summary>
        public override void Reset()
        {
        }
    }
}
