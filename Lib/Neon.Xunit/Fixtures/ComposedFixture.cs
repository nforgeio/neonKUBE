//-----------------------------------------------------------------------------
// FILE:	    ComposedFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Linq;

using Neon.Common;
using Neon.Kube.Service;

namespace Neon.Xunit
{
    /// <summary>
    /// Implements an <see cref="ITestFixture"/> that is composed of other test
    /// fixtures.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The base Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Docker container, modifying the local DNS <b>hosts</b> file, configuring
    /// environment variables or initializing a test database.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// </note>
    /// <para>
    /// Derived test fixtures that modify global machine or other environmental state
    /// must implement a <c>public static void EnsureReset()</c> method resets the state
    /// to a reasonable default.  These will be reflected and called when the first
    /// <see cref="TestFixture"/> is created by the test runner for every test class.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class ComposedFixture : TestFixture, IEnumerable<KeyValuePair<string, ITestFixture>>
    {
        private Dictionary<string, ITestFixture>    nameToFixture;
        private List<ITestFixture>                  fixtureList;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ComposedFixture()
        {
            nameToFixture = new Dictionary<string, ITestFixture>(StringComparer.InvariantCultureIgnoreCase);
            fixtureList   = new List<ITestFixture>();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ComposedFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Adds a named <see cref="ITestFixture"/>.
        /// </summary>
        /// <typeparam name="TFixture">The new fixture type.</typeparam>
        /// <param name="name">The fixture name (case insenstitive).</param>
        /// <param name="subFixture">The subfixture instance.</param>
        /// <param name="action">The optional <see cref="Action"/> to be called when the fixture is initialized.</param>
        /// <remarks>
        /// <note>
        /// This method doesn't work for <see cref="KubeServiceFixture{TService}"/> based fixtures.  Use
        /// <see cref="AddServiceFixture{TService}(string, KubeServiceFixture{TService}, Func{TService})"/> instead.
        /// </note>
        /// </remarks>
        public void AddFixture<TFixture>(string name, TFixture subFixture, Action<TFixture> action = null)
            where TFixture : ITestFixture
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(subFixture != null, nameof(subFixture));
            Covenant.Requires<InvalidOperationException>(!subFixture.IsRunning, "A subfixture cannot be added after it has already been initialized.");

            var fixtureType = typeof(TFixture);

            if (fixtureType.IsGenericType && fixtureType.Name == typeof(KubeServiceFixture<KubeService>).Name)
            {
                throw new InvalidOperationException($"This method doesn't work for [{nameof(KubeServiceFixture<KubeService>)}] fixtures.  Use [AddServiceFixture<TService>()] instead.");
            }

            CheckDisposed();
            CheckWithinAction();

            subFixture.Start(() => action?.Invoke(subFixture));
            nameToFixture.Add(name, subFixture);
            fixtureList.Add(subFixture);
        }

        /// <summary>
        /// Adds a named <see cref="KubeServiceFixture{TService}"/> fixture.
        /// </summary>
        /// <typeparam name="TService">The service type (derived from <see cref="KubeService"/>.</typeparam>
        /// <param name="name">The fixture name (case insenstitive).</param>
        /// <param name="subFixture">The subfixture being added.</param>
        /// <param name="serviceCreator">Callback that creates and returns the new service instance.</param>
        public void AddServiceFixture<TService>(string name, KubeServiceFixture<TService> subFixture, Func<TService> serviceCreator)
            where TService : KubeService
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(subFixture != null, nameof(subFixture));
            Covenant.Requires<InvalidOperationException>(!subFixture.IsRunning, "A subfixture cannot be added after it has already been initialized.");

            CheckDisposed();
            CheckWithinAction();

            subFixture.Start(serviceCreator);
            nameToFixture.Add(name, subFixture);
            fixtureList.Add(subFixture);
        }

        /// <summary>
        /// Starts the fixture if it hasn't already been started including invoking the optional
        /// <see cref="Action"/> when the first time <see cref="Start(Action)"/> is called for
        /// a fixture instance.
        /// </summary>
        /// <param name="action">
        /// <para>
        /// The optional custom start action.
        /// </para>
        /// <note>
        /// This is generally intended for use when developing custom test fixtures.
        /// </note>
        /// </param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if this is called from within the <see cref="Action"/>.</exception>
        public override TestFixtureStatus Start(Action action = null)
        {
            CheckDisposed();

            if (InAction)
            {
                throw new InvalidOperationException($"[{nameof(Start)}()] cannot be called recursively from within the fixture initialization action.");
            }

            if (IsRunning)
            {
                return TestFixtureStatus.AlreadyRunning;
            }

            // Initialize this fixture.

            try
            {
                InAction = true;

                action?.Invoke();
            }
            finally
            {
                InAction      = false;
                IsRunning = true;       // Setting this even if the action failed.
            }

            return TestFixtureStatus.Started;
        }

        /// <summary>
        /// Returns the subfixtures.
        /// </summary>
        public IEnumerable<ITestFixture> Children
        {
            get { return fixtureList; }
        }

        /// <summary>
        /// Returns the named test fixture.
        /// </summary>
        /// <param name="name">The fixture name (case insensitive).</param>
        /// <returns>The test fixture.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named fixture does not exist.</exception>
        public ITestFixture this[string name]
        {
            get
            {
                CheckDisposed();
                return nameToFixture[name];
            }
        }

        /// <summary>
        /// Returns the fixture at the specified index (based on the order
        /// the fixture was added.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>The test fixture.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="index"/> is outside the range of valid indexes.</exception>
        public ITestFixture this[int index]
        {
            get
            {
                CheckDisposed();
                return fixtureList[index];
            }
        }

        /// <summary>
        /// Returns the number of fixtures in the set.
        /// </summary>
        public int Count
        {
            get { return fixtureList.Count; }
        }

        /// <summary>
        /// Disposes all fixtures in the set.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Note that we're going to dispose the subfixtures in the
                // reversed order from how they were created to avoid any 
                // dependancy conflicts.

                foreach (var fixture in fixtureList.Reverse<ITestFixture>())
                {
                    fixture.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            // Reset all of the subfixtures.

            foreach (var fixture in fixtureList.Reverse<ITestFixture>())
            {
                fixture.Reset();
            }
        }

        /// <summary>
        /// Enumerates the named test fixtures in the set.
        /// </summary>
        /// <returns>The fixtures as <c>KeyValuePair&lt;string, ITestFixture&gt;</c> instances.</returns>
        public IEnumerator<KeyValuePair<string, ITestFixture>> GetEnumerator()
        {
            return nameToFixture.GetEnumerator();
        }

        /// <summary>
        /// Enumerates the named test fixtures in the set.
        /// </summary>
        /// <returns>The fixtures as <c>KeyValuePair&lt;string, ITestFixture&gt;</c> instances.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return nameToFixture.GetEnumerator();
        }
    }
}
