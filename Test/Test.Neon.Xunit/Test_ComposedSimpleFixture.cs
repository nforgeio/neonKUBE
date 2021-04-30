//-----------------------------------------------------------------------------
// FILE:	    Test_ComposedSimpleFixture.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestXunit
{
    /// <summary>
    /// Verify that a test fixture composed of other fixtures works.
    /// </summary>
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ComposedSimpleFixture : IClassFixture<ComposedFixture>
    {
        //---------------------------------------------------------------------
        // Private types

        private class Fixture0 : TestFixture
        {
            public bool PublicIsDisposed => base.IsDisposed;
            public bool PublicInAction => base.InAction;
        }

        private class Fixture1 : TestFixture
        {
            public bool PublicIsDisposed => base.IsDisposed;
            public bool PublicInAction => base.InAction;
        }

        //---------------------------------------------------------------------
        // Implementation

        private ComposedFixture     fixture;
        private ITestFixture        fixture0;
        private ITestFixture        fixture1;
        private bool                fixture0Initialized;
        private bool                fixture1Initialized;

        public Test_ComposedSimpleFixture(ComposedFixture fixture)
        {
            this.fixture = fixture;

            fixture.Start(
                () =>
                {
                    fixture.AddFixture("zero", fixture0 = new Fixture0(),
                        subFixture =>
                        {
                            fixture0Initialized = true;
                        });

                    fixture.AddFixture("one", fixture1 = new Fixture1(),
                        subFixture =>
                        {
                            fixture1Initialized = true;
                        });

                    // Ensure that the subfixtures were initialized first
                    // and that their actions were called.

                    Covenant.Assert(fixture0Initialized);
                    Covenant.Assert(fixture1Initialized);

                    // Ensure that the subfixture indexers work.

                    Covenant.Assert(fixture[0] == fixture0);
                    Covenant.Assert(fixture[1] == fixture1);
                    Covenant.Assert(fixture["zero"] == fixture0);
                    Covenant.Assert(fixture["one"] == fixture1);
                });
        }

        /// <summary>
        /// Verify that fixtures look OK.
        /// </summary>
        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void Verify()
        {
            // Ensure that the subfixture indexers work.

            Assert.True(fixture[0] == fixture0);
            Assert.True(fixture[1] == fixture1);
            Assert.True(fixture["zero"] == fixture0);
            Assert.True(fixture["one"] == fixture1);

            // Ensure that the enumerator and count works.

            Assert.Equal(2, fixture.Count);

            var list = new List<KeyValuePair<string, ITestFixture>>();

            foreach (var subfixture in fixture)
            {
                list.Add(subfixture);
            }

            Assert.Equal(2, list.Count);

            Assert.Equal("zero", list[0].Key);
            Assert.Equal(fixture0, list[0].Value);

            Assert.Equal("one", list[1].Key);
            Assert.Equal(fixture1, list[1].Value);
        }
    }
}
