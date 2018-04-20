//-----------------------------------------------------------------------------
// FILE:	    TestFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

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
    /// </remarks>
    /// <threadsafety instance="false"/>
    public abstract class TestFixture : ITestFixture
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// 
        /// </summary>
        public static void Reset()
        {
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
            Dispose(true);

            IsDisposed = true;
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
