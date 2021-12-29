//-----------------------------------------------------------------------------
// FILE:	    AssemblyInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

using Xunit;

// Disable parallel test execution because [TestFixture] doesn't
// support this in general.

[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]