//-----------------------------------------------------------------------------
// FILE:	    ITestFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Couchbase;

using Neon.Common;

namespace Xunit
{
    /// <summary>
    /// Defines the behavior of a Neon Xunit test fixture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Xunit test fixtures are designed to provide initialize global state 
    /// that tests can then reference during their execution.  Typical 
    /// scenarios include:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Starting a database server and prepopulating it with a schema
    /// and data to test database access code.
    /// </item>
    /// <item>
    /// Starting a Docker service such that REST endpoints can be tested.
    /// </item>
    /// <item>
    /// Initializing a neonCLUSTER and then configuring it with certificates,
    /// routes, services etc. and then performing tests against the
    /// actual cluster.
    /// </item>
    /// </list>
    /// <para>
    /// Test fixture lifecycle:
    /// </para>
    /// <list type="number">
    /// <item>
    /// First, you'll need create your Xunit test class and have it derive
    /// from <see cref="IClassFixture{TFixture}"/>, where <c>TFixture</c>
    /// identifies the fixture.
    /// </item>
    /// <item>
    /// The Xunit test runner reflects the test assemblies and identifies the
    /// test classes with <c>[Fact]</c> test methods to be executed.
    /// </item>
    /// <item>
    /// For each test class to be executed, the test runner first creates
    /// an instance of the test fixture.  This is created <b>before</b>
    /// one before any of the test classes are instantiated and any
    /// test methods are called.
    /// </item>
    /// <item>
    /// <para>
    /// The test runner creates a new instance of the test class for each
    /// test method to be invoked.  The test class constructor must accept
    /// a single parameter with type <c>TFixture</c>.  The test class 
    /// constructor should call <see cref="ITestFixture.Initialize(Action)"/>
    /// to initialize the fixture, passing an optional <see cref="Action"/>
    /// that does any custom initialization for the test.
    /// </para>
    /// <para>
    /// Test fixtures are designed to be aware of whether they've been
    /// initialized or not such that only the first call to
    /// <see cref="ITestFixture.Initialize(Action)"/> will perform any
    /// necessary initialization (including calling the custom action)
    /// and any subsequent calls will do nothing.
    /// </para>
    /// </item>
    /// <item>
    /// The test runner will continue instantiating test class instances
    /// and calling test methods using the test fixture state setup
    /// during the first test.
    /// </item>
    /// <item>
    /// Once all of the test methods have been called, the test runner
    /// will call the test fixtures <see cref="IDisposable.Dispose()"/>
    /// method so that it can clean up any state.
    /// </item>
    /// </list>
    /// </remarks>
    public interface ITestFixture : IDisposable
    {
        /// <summary>
        /// Invokes a parameterless <see cref="Action"/> when the fixture
        /// is not already initialized.
        /// </summary>
        /// <param name="action">The optional initialization action.</param>
        void Initialize(Action action = null);
    }
}