//-----------------------------------------------------------------------------
// FILE:	    TestFixtureSet.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Neon.Common;

namespace Neon.Xunit
{
    /// <summary>
    /// Implements an <see cref="ITestFixture"/> that is a list of other test
    /// fixtures, providing an easy way to compose multiple separate fixtures
    /// into a single combined fixture.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or neonHIVE.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file with:
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true)]
    /// </code>
    /// </para>
    /// </note>
    /// <para>
    /// Serrived test fixtures that modify global machine or other environmental state
    /// must implement a <c>public static void EnsureReset()</c> method resets the state
    /// to a reasonable default.  These will be reflected and called when the first
    /// <see cref="TestFixture"/> is created by the test runner for every test class.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class TestFixtureSet : TestFixture, IEnumerable<KeyValuePair<string, ITestFixture>>
    {
        private Dictionary<string, ITestFixture>    nameToFixture;
        private List<ITestFixture>                  fixtureList;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TestFixtureSet()
        {
            nameToFixture = new Dictionary<string, ITestFixture>(StringComparer.InvariantCultureIgnoreCase);
            fixtureList   = new List<ITestFixture>();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~TestFixtureSet()
        {
            Dispose(false);
        }

        /// <summary>
        /// Adds a named <see cref="ITestFixture"/> to the set.
        /// </summary>
        /// <param name="name">The fixture name (case insenstitive).</param>
        /// <param name="subFixture">The subfixture instance.</param>
        /// <param name="action">The optional <see cref="Action"/> to be called when the fixture is initialized.</param>
        public void AddFixture<TFixture>(string name, TFixture subFixture, Action<TFixture> action = null)
            where TFixture : ITestFixture
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(subFixture != null);
            Covenant.Requires<InvalidOperationException>(!subFixture.IsInitialized, "A subfixture cannot be added after it has already been initialized.");

            CheckDisposed();
            CheckWithinAction();

            subFixture.Initialize(() => action?.Invoke(subFixture));
            nameToFixture.Add(name, subFixture);
            fixtureList.Add(subFixture);
        }

        /// <summary>
        /// Initializes the fixture if it hasn't already been intialized
        /// including invoking the optional <see cref="Action"/>.
        /// </summary>
        /// <param name="action">The optional initialization action.</param>
        /// <returns>
        /// <c>true</c> if the fixture wasn't previously initialized and
        /// this method call initialized it or <c>false</c> if the fixture
        /// was already initialized.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if this is called from within the <see cref="Action"/>.</exception>
        /// <remarks>
        /// This method works by calling the initialization methods for each
        /// of the subfixtures in the order they were added and then calling 
        /// the optional <see cref="Action"/> afterwards.
        /// </remarks>
        public override bool Initialize(Action action = null)
        {
            CheckDisposed();

            if (InAction)
            {
                throw new InvalidOperationException($"[{nameof(Initialize)}()] cannot be called recursively from within the fixture initialization action.");
            }

            if (IsInitialized)
            {
                return false;
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
                IsInitialized = true;       // Setting this even if the action failed.
            }

            return true;
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
