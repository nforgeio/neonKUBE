//-----------------------------------------------------------------------------
// FILE:	    ClusterFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;

using YamlDotNet.RepresentationModel;

using Neon.Cluster;
using Neon.Common;

namespace Xunit
{
    /// <summary>
    /// An Xunit test fixture used to run unit tests on a neonCLUSTER.
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
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true)]
    /// </code>
    /// </note>
    /// <para>
    /// This Xunit test fixture is used
    /// </para>
    /// <para>
    /// neonCLUSTERs do not allow the <see cref="ClusterFixture"/> to perform unit
    /// tests by default, as a safety measure.  You can enable this before cluster
    /// deployment by setting <see cref="ClusterDefinition.AllowUnitTesting"/><c>=true</c>
    /// or by manually invoking this command for an existing cluster:
    /// </para>
    /// <code>
    /// neon cluster set allow-unit-testing=yes
    /// </code>
    /// <para>
    /// The specified cluster login file must be already present on the current
    /// machine for the current user.  This method will logout from the current
    /// cluster (if any) and then login to the one specified.
    /// </para>
    /// <para>
    /// There are two basic patterns for using this fixture.
    /// </para>
    /// <list type="table">
    /// <item>
    /// <term><b>initialize once</b></term>
    /// <description>
    /// <para>
    /// The basic idea here is to have your test class initialize the cluster
    /// once within the test class constructor inside of the initialize action
    /// with common state and services that all of the tests can access.
    /// </para>
    /// <para>
    /// This will be quite a bit faster than reconfiguring the cluster at the
    /// beginning of every test and can work well for many situations.
    /// </para>
    /// </description>
    /// </item>
    /// <item>
    /// <term><b>initialize every test</b></term>
    /// <description>
    /// For scenarios where the cluster must be cleared before every test,
    /// you can use the <see cref="Reset()"/> method to reset its
    /// state within each test method, populate the cluster as necessary,
    /// and then perform your tests.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class ClusterFixture : DockerFixture
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to track how many fixture instances for the current test run
        /// remain so we can determine when to reset the cluster.
        /// </summary>
        private static int RefCount = 0;

        //---------------------------------------------------------------------
        // Instance members
        
        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public ClusterFixture()
        {
            if (RefCount++ == 0)
            {
                Reset();
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ClusterFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (!base.IsDisposed)
            {
                if (--RefCount <= 0)
                {
                    Reset();
                }

                Covenant.Assert(RefCount >= 0, "Reference count underflow.");
            }
        }

        /// <summary>
        /// Connects the fixture to a cluster.
        /// </summary>
        /// <param name="login">The cluster login, like: <b>USER@CLUSTER</b>.</param>
        /// <remarks>
        /// <note>
        /// <para>
        /// neonCLUSTERs do not allow the <see cref="ClusterFixture"/> to perform unit
        /// tests by default, as a safety measure.  You can enable this before cluster
        /// deployment by setting <see cref="ClusterDefinition.AllowUnitTesting"/><c>=true</c>
        /// or by manually invoking this command for an existing cluster:
        /// </para>
        /// <code>
        /// neon cluster set allow-unit-testing=yes
        /// </code>
        /// </note>
        /// <para>
        /// The specified <paramref name="login"/> must be already present on the current
        /// machine for the current user.  This method will logout from the current cluster
        /// (if any) and then login to the specified cluster.
        /// </para>
        /// </remarks>
        public void Login(string login)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(login));

            throw new NotImplementedException("$todo(jeff.lill)");
        }

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command on a cluster manager, 
        /// passing unformatted arguments and returns the results.
        /// </summary>
        /// <param name="args">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method formats any arguments passed so they will be suitable 
        /// for passing on the command line by quoting and escaping them
        /// as necessary.
        /// </para>
        /// </remarks>
        public override ExecuteResult DockerExecute(params object[] args)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var neonArgs = new List<object>();

                neonArgs.Add("docker");
                neonArgs.Add("--");

                foreach (var item in args)
                {
                    neonArgs.Add(item);
                }

                return NeonHelper.ExecuteCaptureStreams("neon", neonArgs.ToArray());
            }
        }

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command on a cluster manager, 
        /// passing  a pre-formatted argument string and returns the results.
        /// </summary>
        /// <param name="argString">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method assumes that the single string argument passed is already
        /// formatted as required to pass on the command line.
        /// </para>
        /// </remarks>
        public override ExecuteResult DockerExecute(string argString)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                var neonArgs = "docker -- " + argString;

                return NeonHelper.ExecuteCaptureStreams("docker", neonArgs);
            }
        }

        /// <summary>
        /// Executes an arbitrary <b>neon</b> CLI command passing unformatted
        /// arguments and returns the results.
        /// </summary>
        /// <param name="args">The <b>neon</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method formats any arguments passed so they will be suitable 
        /// for passing on the command line by quoting and escaping them
        /// as necessary.
        /// </para>
        /// </remarks>
        public virtual ExecuteResult NeonExecute(params object[] args)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                return NeonHelper.ExecuteCaptureStreams("neon", args);
            }
        }

        /// <summary>
        /// Executes an arbitrary <b>neon</b> CLI command passing a pre-formatted 
        /// argument string and returns the results.
        /// </summary>
        /// <param name="argString">The <b>neon</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method assumes that the single string argument passed is already
        /// formatted as required to pass on the command line.
        /// </para>
        /// </remarks>
        public virtual ExecuteResult NeonExecute(string argString)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();

                return NeonHelper.ExecuteCaptureStreams("neon", argString);
            }
        }

        /// <summary>
        /// Resets the local Docker daemon by clearing all swarm services and state
        /// as well as removing all containers.
        /// </summary>
        /// <remarks>
        /// <note>
        /// As a safety measure, this method ensures that the local Docker instance
        /// <b>IS NOT</b> a member of a multi-node swarm to avoid wiping out production
        /// clusters by accident.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        /// <exception cref="InvalidOperationException">Thrown if the local Docker instance is a member of a multi-node swarm.</exception>
        public new void Reset()
        {
            // $todo(jeff.lill):
            //
            // I'm not going to worry about removing any containers just yet.
            // Presumably, we'd leave any [neon-*] related containers running 
            // by default and remove all other non-task (service or stack)
            // containers on all nodes.  One thing to think about is whether
            // this should apply to pet nodes as well.
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <param name="image">Specifies the container image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker service create ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the service.</param>
        /// <param name="env">Optional environment variables to be passed to the container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new void CreateContainer(string name, string image, string[] dockerArgs = null, string[] containerArgs = null, string[] env = null)
        {
            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <returns>A list of <see cref="DockerFixture.ContainerInfo"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new List<ContainerInfo> ListContainers()
        {
            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new void RemoveContainer(string name)
        {
            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }
    }
}
