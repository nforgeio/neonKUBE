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

namespace Xunit
{
    /// <summary>
    /// Implements an <see cref="ITestFixture"/> that is a list of other test
    /// fixtures, providing an easy way to compose multiple separate fixtures
    /// into a single combined fixture.
    /// </summary>
    /// <threadsafety instance="false"/>
    public class TestFixtureSet : TestFixture, IEnumerable<KeyValuePair<string, ITestFixture>>
    {
        private Dictionary<string, ITestFixture>    nameToFixture;
        private List<ITestFixture>                  fixtureList;
        private List<Action>                        actionList;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TestFixtureSet()
        {
            nameToFixture = new Dictionary<string, ITestFixture>(StringComparer.InvariantCultureIgnoreCase);
            fixtureList   = new List<ITestFixture>();
            actionList    = new List<Action>();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~TestFixtureSet()
        {
            Dispose(false);
        }

        /// <summary>
        /// Adds a named fixture to the set.
        /// </summary>
        /// <param name="name">The fixture name (case insenstitive).</param>
        /// <param name="fixture">The fixture instance.</param>
        /// <param name="action">The optional <see cref="Action"/> to be called when the fixture is initialized.</param>
        public void Add(string name, ITestFixture fixture, Action action = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(fixture != null);
            Covenant.Requires<InvalidOperationException>(!fixture.IsInitialized, "Subfixtures cannot be added after the fixture has been initialized.");

            nameToFixture.Add(name, fixture);
            fixtureList.Add(fixture);
            actionList.Add(action);
        }

        /// <summary>
        /// Initializes the fixture if it hasn't already been intialized
        /// including invoking the optional <see cref="Action"/>.
        /// </summary>
        /// <param name="action">The optional initialization action.</param>
        /// <exception cref="InvalidOperationException">Thrown if this is called from within the <see cref="Action"/>.</exception>
        /// <remarks>
        /// This method works by calling the initialization methods for each
        /// of the subfixtures in the order they were added and then calling 
        /// the optional <see cref="Action"/> afterwards.
        /// </remarks>
        public override void Initialize(Action action = null)
        {
            CheckDisposed();

            if (InAction)
            {
                throw new InvalidOperationException($"[{nameof(Initialize)}()] cannot be called recursively from within the fixture initialization action.");
            }

            if (IsInitialized)
            {
                return;
            }

            // Initialize the subfixtures.

            for (int i = 0; i < Count; i++)
            {
                var subFixture = fixtureList[i];
                var subAction  = actionList[i];

                subFixture.Initialize(subAction);
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
        }

        /// <summary>
        /// Returns the named test fixture.
        /// </summary>
        /// <param name="name">The fixture name (case insensitive).</param>
        /// <returns>The test fixture.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named fixture does not exist.</exception>
        public ITestFixture this[string name]
        {
            get { return nameToFixture[name]; }
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
            get { return fixtureList[index]; }
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
            // Note that we're going to dispose the subfixtures in the
            // reversed order from how they were created to avoid any 
            // dependancy conflicts.

            foreach (var fixture in fixtureList.Reverse<ITestFixture>())
            {
                fixture.Dispose();
            }

            base.Dispose(disposing);
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
