//-----------------------------------------------------------------------------
// FILE:	    ComposedFixture.cs
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
using System.Linq;
using System.Reflection;
using System.Threading;

using Neon.Common;
using Neon.Service;

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
    /// support parallel test execution.  You need to explicitly disable parallel execution in 
    /// all test assemblies that rely on thesex test fixtures by adding a C# file called 
    /// <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// <para>
    /// and then define your test classes like:
    /// </para>
    /// <code language="csharp">
    /// public class MyTests : IClassFixture&lt;ComposedFixture&gt;, IDisposable
    /// {
    ///     [Collection(TestCollection.NonParallel)]
    ///     [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    ///     [Fact]
    ///     public void Test()
    ///     {
    ///     }
    /// }
    /// </code>
    /// </note>
    /// <para>
    /// Derived test fixtures that modify global machine or other environmental state
    /// must implement a <c>public static void EnsureReset()</c> method resets the state
    /// to a reasonable default.  These will be reflected and called when the first
    /// <see cref="TestFixture"/> is created by the test runner for every test class.
    /// </para>
    /// <para><b>INTEGRATION TESTING</b></para>
    /// <para>
    /// One use case we've found valuable is to use <see cref="ComposedFixture"/> to enulate
    /// an entire cluster of services as a unit test or in a console application.  The idea
    /// is to have the unit test or console app code reference all of your service assemblies
    /// and then add these services to a <see cref="ComposedFixture"/> as well as any database
    /// and/or workflow engines and then start the composed fixtures.
    /// </para>
    /// <para>
    /// This can require a lot of memory and CPU, but it can be really nice to have an entire
    /// service running in Visual Studio where you can set breakpoints anywhere.  We've emulated
    /// clusters with well over 75 services this way.
    /// </para>
    /// <para>
    /// One of the problems we encountered is that it can take several minutes for the all of
    /// the services and other subfixtures to start because they are started one at a time
    /// by default.  We've enhanced this class so that you can optionally start groups of 
    /// subfixtures in parallel via the optional <c>group</c> parameter.  By default,
    /// this is passed as <b>-1</b>, indicating that subfixtures with <c>group=-1</c> will
    /// be started one at a time in the group they were added to the <see cref="ComposedFixture"/>
    /// and these will be started before any other fixtures.  This results in the same behavior
    /// as older versions of the fixture.
    /// </para>
    /// <para>
    /// Fixtures added with <c>group</c> passed as zero or a positive number are started when
    /// you call <see cref="Start(Action)"/>.  This starts the subfixtures in the same group in
    /// parallel with any others in the group.  Note that we'll start at the lowest group number 
    /// and wait for all fixtures to start before moving on to the next group.
    /// </para>
    /// <para>
    /// <see cref="CodeFixture"/> can be used as a way to inject custom code what will
    /// be executed while <see cref="ComposedFixture"/> is starting subfixtures.  The basic
    /// idea is to add things like database fixtures as <b>group=0</b> and then add a
    /// <see cref="CodeFixture"/> with a custom action as <b>group=1</b> followed by
    /// <see cref="NeonServiceFixture{TService}"/> and/or other fixtures as <b>group=2+</b>.
    /// </para>
    /// <para>
    /// Then the <see cref="ComposedFixture"/> will start the database first, followed by the
    /// <see cref="CodeFixture"/> where the action has an opportunity to initialize the database
    /// before the remaining fixtures are started.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class ComposedFixture : TestFixture, IEnumerable<KeyValuePair<string, ITestFixture>>
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds information about a NeonServ subfixture.
        /// </summary>
        private struct SubFixture
        {
            public readonly ITestFixture    Fixture;
            public readonly bool            IsNeonService;
            public readonly object          ActionTarget;
            public readonly MethodInfo      ActionMethod;
            public readonly object          ServiceCreator;
            public readonly ServiceMap      ServiceMap;
            public readonly TimeSpan        StartTimeout;
            public readonly int             Group;

            /// <summary>
            /// Constructor for non-<see cref="NeonServiceFixture{TService}"/> fixtures.
            /// </summary>
            /// <param name="fixture">The subfixture.</param>
            /// <param name="actionTarget">The optional fixture action instance.</param>
            /// <param name="actionMethod">The optional fixture action method.</param>
            /// <param name="group">The fixture group.</param>
            public SubFixture(ITestFixture fixture, object actionTarget, MethodInfo actionMethod, int group)
            {
                // $hack(jefflill):
                //
                // I'm having the caller pass the action instance and method manually to
                // avoid casting errors.  There's probably a cleaner way to do this but
                // this'll work fine.

                this.Fixture        = fixture;
                this.IsNeonService  = false;
                this.ActionTarget   = actionTarget;
                this.ActionMethod   = actionMethod;
                this.ServiceCreator = null;
                this.ServiceMap     = null;
                this.StartTimeout   = default;
                this.Group          = group;
            }

            /// <summary>
            /// Constructor for <see cref="NeonServiceFixture{TService}"/> fixtures.
            /// </summary>
            /// <param name="fixture">The subfixture.</param>
            /// <param name="serviceCreator">The service creator function as an object.</param>
            /// <param name="serviceMap">Specifies the service map, if any.</param>
            /// <param name="startTimeout">Specifies the maximum time to wait for the service to transition to the running state.</param>
            /// <param name="group">The fixture group.</param>
            public SubFixture(ITestFixture fixture, object serviceCreator, ServiceMap serviceMap, TimeSpan startTimeout, int group)
            {
                // $hack(jefflill):
                //
                // I'm having the caller pass the action instance and method manually to
                // avoid casting errors.  There's probably a cleaner way to do this but
                // this'll work fine.

                this.Fixture        = fixture;
                this.IsNeonService  = true;
                this.ActionTarget   = null;
                this.ActionMethod   = null;
                this.ServiceCreator = serviceCreator;
                this.StartTimeout   = startTimeout;
                this.ServiceMap     = serviceMap;
                this.Group          = group;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private Dictionary<string, ITestFixture>    nameToFixture;
        private List<SubFixture>                    fixtureList;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ComposedFixture()
        {
            nameToFixture = new Dictionary<string, ITestFixture>(StringComparer.InvariantCultureIgnoreCase);
            fixtureList   = new List<SubFixture>();
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
        /// <param name="action">
        /// The optional <see cref="Action"/> to be called when the fixture is initialized.  This can
        /// be used for things like waiting until the service is actually ready before returning.
        /// </param>
        /// <param name="group">
        /// Optionally specifies the fixture group.  Fixtures with <paramref name="group"/><c>=-1</c> (the default)
        /// will be started one by one before all other fixtures.  Fixtures with a <c>group >= 0</c> will
        /// be started in parallel by group starting at the lowest group.  All of the fixtures in the same
        /// group will be started in parallel on separate threads and the <see cref="ComposedFixture"/> will
        /// wait until all fixtures in a group have started before advancing to the next group.
        /// </param>
        /// <remarks>
        /// <note>
        /// This method doesn't work for <see cref="NeonServiceFixture{TService}"/> based fixtures.  Use
        /// <see cref="AddServiceFixture{TService}(string, NeonServiceFixture{TService}, Func{TService}, ServiceMap, TimeSpan, int)"/> instead.
        /// </note>
        /// </remarks>
        public void AddFixture<TFixture>(string name, TFixture subFixture, Action<TFixture> action = null, int group = -1)
            where TFixture : class, ITestFixture
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(subFixture != null, nameof(subFixture));
            Covenant.Requires<InvalidOperationException>(!subFixture.IsRunning, "A subfixture cannot be added after it has already been initialized.");
            Covenant.Requires<ArgumentException>(group >= -1, nameof(group));

            var fixtureType = typeof(TFixture);

            if (fixtureType.IsGenericType && fixtureType.FullName == typeof(NeonServiceFixture<NeonService>).FullName)
            {
                throw new InvalidOperationException($"This method doesn't work for [{nameof(NeonServiceFixture<NeonService>)}] fixtures.  Use [AddServiceFixture<TService>(...)] instead.");
            }

            CheckDisposed();
            CheckWithinAction();

            nameToFixture.Add(name, subFixture);
            fixtureList.Add(new SubFixture(subFixture, action?.Target, action?.Method, group));

            if (group == -1)
            {
                subFixture.Start(() => action?.Invoke(subFixture));
            }
        }

        /// <summary>
        /// Adds a named <see cref="NeonServiceFixture{TService}"/> fixture.
        /// </summary>
        /// <typeparam name="TService">The service type (derived from <see cref="NeonService"/>).</typeparam>
        /// <param name="name">The fixture name (case insenstitive).</param>
        /// <param name="subFixture">The subfixture being added.</param>
        /// <param name="serviceCreator">
        /// <para>
        /// Callback that creates and returns the new service instance.
        /// </para>
        /// </param>
        /// <param name="serviceMap">
        /// Optionally specifies a <see cref="ServiceMap"/>.  When a service map is passed and there's
        /// a <see cref="ServiceDescription"/> for the created service, then the fixture will configure
        /// the service with <see cref="ServiceDescription.TestEnvironmentVariables"/>, <see cref="ServiceDescription.TestBinaryConfigFiles"/>,
        /// and <see cref="ServiceDescription.TestTextConfigFiles"/> before starting the service.
        /// </param>
        /// <param name="startTimeout">
        /// Optionally specifies maximum time to wait for the service to transition to the running state.
        /// </param>
        /// <param name="group">
        /// Optionally specifies the fixture group.  Fixtures with <paramref name="group"/><c>=-1</c> (the default)
        /// will be started one by one before all other fixtures.  Fixtures with a <c>group >= 0</c> will
        /// be started in parallel by group starting at the lowest group.  All of the fixtures in the same
        /// group will be started in parallel on separate threads and the <see cref="ComposedFixture"/> will
        /// wait until all fixtures in a group have started before advancing to the next group.
        /// </param>
        public void AddServiceFixture<TService>(string name, NeonServiceFixture<TService> subFixture, Func<TService> serviceCreator, ServiceMap serviceMap = null, TimeSpan startTimeout = default, int group = -1)
            where TService : NeonService
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(subFixture != null, nameof(subFixture));
            Covenant.Requires<ArgumentNullException>(serviceCreator != null, nameof(serviceCreator));
            Covenant.Requires<InvalidOperationException>(!subFixture.IsRunning, "A subfixture cannot be added after it has already been initialized.");
            Covenant.Requires<ArgumentException>(group >= -1, nameof(group));

            CheckDisposed();
            CheckWithinAction();

            nameToFixture.Add(name, subFixture);
            fixtureList.Add(new SubFixture(subFixture, serviceCreator, serviceMap, startTimeout, group));

            if (group == -1)
            {
                subFixture.Start(serviceCreator, serviceMap);
            }
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

                // Start any fixture groups from lowest to highest in parallel
                // on separate threads.

                var groups = new HashSet<int>();

                foreach (var group in fixtureList
                    .Where(fixture => fixture.Group >= 0)
                    .Select(fixture => fixture.Group))
                {
                    if (!groups.Contains(group))
                    {
                        groups.Add(group);
                    }
                }

                if (groups.Count > 0)
                {
                    var fixtureThreads    = new List<Thread>();
                    var fixtureExceptions = new List<Exception>();

                    foreach (var group in groups.OrderBy(group => group))
                    {
                        foreach (var subFixture in fixtureList.Where(fixture => fixture.Group == group))
                        {
                            var thread = new Thread(
                                new ParameterizedThreadStart(
                                    arg =>
                                    {
                                        try
                                        {
                                            var sf = (SubFixture)arg;

                                            if (sf.IsNeonService)
                                            {
                                                // $hack(jefflill):
                                                //
                                                // Using reflection, locate the correct subfixture [Start(Func<TService>, TimeSpan] 
                                                // method and then call it, passing the service creator function and running timeout.

                                                var startMethod = (MethodInfo)null;

                                                foreach (var method in sf.Fixture.GetType().GetMethods().Where(mi => mi.Name == "Start"))
                                                {
                                                    var paramTypes = method.GetParameterTypes();

                                                    if (paramTypes.Length != 3)
                                                    {
                                                        continue;
                                                    }

                                                    if (paramTypes[0].Name == "Func`1" && paramTypes[1] == typeof(ServiceMap) && paramTypes[2] == typeof(TimeSpan))
                                                    {
                                                        startMethod = method;
                                                        break;
                                                    }
                                                }

                                                Covenant.Assert(startMethod != null, "The fixture's [Start(Func<TService>, ServiceMap, TimeSpan)] method signature must have changed.");

                                                startMethod.Invoke(sf.Fixture, new object[] { sf.ServiceCreator, sf.ServiceMap, sf.StartTimeout });
                                            }
                                            else
                                            {
                                                // $hack(jefflill): Using reflection to call the fixture's start method.

                                                sf.Fixture.Start(() => sf.ActionMethod?.Invoke(sf.ActionTarget, new object[] { subFixture.Fixture }));
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            lock (fixtureExceptions)
                                            {
                                                fixtureExceptions.Add(e);
                                            }
                                        }
                                    }));

                            fixtureThreads.Add(thread);
                            thread.Start(subFixture);

                            // Wait for all fixtured in the group to complete.

                            NeonHelper.WaitAll(fixtureThreads);
                        }

                        if (fixtureExceptions.Count > 0)
                        {
                            throw new AggregateException(fixtureExceptions);
                        }

                        fixtureThreads.Clear();
                    }
                }
            }
            finally
            {
                InAction  = false;
                IsRunning = true;       // Setting this even if the action failed.
            }

            return TestFixtureStatus.Started;
        }

        /// <summary>
        /// Returns the subfixtures.
        /// </summary>
        public IEnumerable<ITestFixture> Children
        {
            get { return fixtureList.Select(fixture => fixture.Fixture); }
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
        /// the fixture was added).
        /// </summary>
        /// <param name="index">Specfies the index of the desired fixture.</param>
        /// <returns>The test fixture.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="index"/> is outside the range of valid indexes.</exception>
        public ITestFixture this[int index]
        {
            get
            {
                CheckDisposed();
                return fixtureList[index].Fixture;
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

                foreach (var fixture in fixtureList.Select(fixture => fixture.Fixture).Reverse<ITestFixture>())
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

            foreach (var fixture in fixtureList.Select(fixture => fixture.Fixture).Reverse<ITestFixture>())
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
