//-----------------------------------------------------------------------------
// FILE:	    TestFixture.cs
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;

using Neon.Common;

namespace Neon.Xunit
{
    /// <summary>
    /// Abstract test fixture base class.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or cluster.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file with:
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// </para>
    /// </note>
    /// <para>
    /// Test fixtures that modify global machine or other environmental state must
    /// implement a <c>public static void EnsureReset()</c> method resets the state
    /// to reasonable defaults.  These will be reflected and called when the first
    /// <see cref="TestFixture"/> is created by the test runner for every test class.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public abstract class TestFixture : ITestFixture
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Holds any reflected <c>public static void EnsureReset()</c> from any
        /// <see cref="TestFixture"/> implementations.
        /// </summary>
        private static List<MethodInfo> resetMethods;

        /// <summary>
        /// Resets the state of any reflected fixture implementations.
        /// </summary>
        private static void EnsureReset()
        {
            // Reflect the test fixture reset methods if we haven't already.

            if (resetMethods == null)
            {
                resetMethods = new List<MethodInfo>();

                foreach (var assembly in AppDomain.CurrentDomain.GetUserAssemblies())
                {
                    Type[] assemblyTypes;

                    try
                    {
                        assemblyTypes = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // We see this for some assemblies like the test runner itself.
                        // We're going to ignore these.

                        assemblyTypes = new Type[0];
                    }

                    foreach (var type in assemblyTypes)
                    {
                        var typeInfo = type.GetTypeInfo();

                        if (typeInfo.ImplementedInterfaces.Contains(typeof(ITestFixture)))
                        {
                            var methodInfo = typeInfo.GetMethod(nameof(ComposedFixture.EnsureReset), BindingFlags.Public | BindingFlags.Static);

                            if (methodInfo == null)
                            {
                                continue;
                            }

                            if (methodInfo.ReturnType == typeof(void) && methodInfo.GetParameters().Length == 0)
                            {
                                resetMethods.Add(methodInfo);
                            }
                        }
                    }
                }
            }

            // Call the reflected reset methods.

            foreach (var method in resetMethods)
            {
                try
                {
                    method.Invoke(null, null);
                }
                catch
                {
                    // Intentionally ignoring any exceptions.
                    // Not sure if anything else makes sense.
                }
            }
        }

        /// <summary>
        /// Used to track whether <see cref="EnsureReset"/> should be called when
        /// the first test fixture is created or when the last one is disposed.
        /// </summary>
        private static int RefCount = 0;

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public TestFixture()
        {
            if (RefCount++ == 0)
            {
                EnsureReset();
            }

            this.IsDisposed = false;
            this.InAction   = false;
            this.IsRunning  = false;
            this.State      = new Dictionary<string, object>();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~TestFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns <c>true</c> if the instance has been disposed.
        /// </summary>
        protected bool IsDisposed { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the <see cref="Start(Action)"/> method
        /// is running.
        /// </summary>
        protected bool InAction { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the fixture has been initialized.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Verifies that the fixture instance has not been disposed.
        /// </summary>
        protected void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }

        /// <summary>
        /// Verifies that the fixture instance's <see cref="Start(Action)"/>
        /// method is executing.
        /// </summary>
        protected void CheckWithinAction()
        {
            if (!InAction)
            {
                throw new InvalidOperationException($"[{this.GetType().Name}] initialization methods may only be called from within the fixture's initialization action.");
            }
        }

        /// <summary>
        /// Starts the fixture if it hasn't already been started including invoking the optional
        /// <see cref="Action"/> when the first time <see cref="Start(Action)"/> is called for
        /// a fixture instance.
        /// </summary>
        /// <param name="action">The optional custom start action.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if this is called from within the <see cref="Action"/>.</exception>
        public virtual TestFixtureStatus Start(Action action = null)
        {
            CheckDisposed();

            if (InAction)
            {
                throw new InvalidOperationException($"[{nameof(Start)}()] cannot be called recursively from within the fixture initialization action.");
            }

            if (IsRunning)
            {
                OnRestart();
                return TestFixtureStatus.AlreadyRunning;
            }

            try
            {
                InAction = true;
                action?.Invoke();
            }
            finally
            {
                InAction  = false;
                IsRunning = true;       // Setting this even if the action failed.
            }

            return TestFixtureStatus.Started;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                try
                {
                    Dispose(true);
                }
                catch
                {
                    // Ignoring any exceptions.
                }

                IsDisposed = true;
                RefCount--;

                Covenant.Assert(RefCount >= 0, "Reference count underflow.");
            }
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public virtual void Reset()
        {
            State.Clear();
        }

        /// <summary>
        /// <para>
        /// Called when an already started fixture is being restarted.  This provides the
        /// fixture an opportunity to do some custom initialization.  This base method
        /// does nothing.
        /// </para>
        /// <note>
        /// This method is intended only for use by test fixture implementations.  Unit
        /// tests or test fixtures should never call this directly.
        /// </note>
        /// </summary>
        public virtual void OnRestart()
        {
        }

        /// <inheritdoc/>
        public IDictionary<string, object> State { get; private set; }
    }
}
