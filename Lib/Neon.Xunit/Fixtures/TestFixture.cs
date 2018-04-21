//-----------------------------------------------------------------------------
// FILE:	    TestFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

using Neon.Common;

namespace Xunit
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
    /// file or managing a Docker Swarm or neonCLUSTER.
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
    /// Test fixtures that modify global machine or other environmental state must
    /// implement a <c>public static void EnsureReset()</c> method resets the state
    /// to a reasonable default.  These will be reflected and called when the first
    /// <see cref="TestFixture"/> is created by the test runner for every test class.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false"/>
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
        private static void Reset()
        {
            // Reflect the test fixture reset methods if we haven't already.

            if (resetMethods == null)
            {
                resetMethods = new List<MethodInfo>();

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
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
                            var methodInfo = typeInfo.GetMethod("EnsureReset", BindingFlags.Public | BindingFlags.Static);

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
        /// Used to track whether <see cref="Reset"/> should be called when
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
                Reset();
            }

            this.SyncRoot      = new object();
            this.IsDisposed    = false;
            this.InAction      = false;
            this.IsInitialized = false;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~TestFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the object to be used for thread synchronization.
        /// </summary>
        protected object SyncRoot { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the instance has been disposed.
        /// </summary>
        protected bool IsDisposed { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the <see cref="Initialize(Action)"/> method
        /// is running.
        /// </summary>
        protected bool InAction { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the fixture has been initialized.
        /// </summary>
        public bool IsInitialized { get; set; }

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
        /// Verifies that the fixture instance's <see cref="Initialize(Action)"/>
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
        /// Initializes the fixture if it hasn't already been intialized
        /// including invoking the optional <see cref="Action"/>.
        /// </summary>
        /// <param name="action">The optional initialization action.</param>
        /// <exception cref="InvalidOperationException">Thrown if this is called from within the <see cref="Action"/>.</exception>
        public virtual void Initialize(Action action = null)
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
    }
}
