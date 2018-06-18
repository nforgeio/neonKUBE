//-----------------------------------------------------------------------------
// FILE:	    IClusterUpdate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Describes the behavior of an update that can be used to upgrade a neonCLUSTER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonCLUSTERs are versioned using the version number of the <b>neon-cli</b> used
    /// to deploy or update the cluster.  An instance of this class will be able to
    /// upgrade the cluster from an older <b>neon-cli</b> version to a newer version.
    /// Multiple updates may need to be applied to upgrade a cluster.
    /// </para>
    /// <para>
    /// Updates are identified by the <see cref="SemanticVersion"/> properties:
    /// <see cref="FromVersion"/> and <see cref="ToVersion"/>.  These refer to the
    /// version of <b>neon-cli</b> that created the cluster or the last version of
    /// the last update.
    /// </para>
    /// <para>
    /// <see cref="FromVersion"/> specifies the earliest cluster version the update
    /// can be applied to and <see cref="ToVersion"/> specifies the cluster version
    /// that will be set after the update is applied.  Note that it's possible to
    /// apply the update to a cluster whose version is greater than or equal to 
    /// <see cref="FromVersion"/> and less than <see cref="ToVersion"/>.  This 
    /// supports the common situation where <b>neon-cli</b> was updated, given
    /// a new version but the changes were client side only, requiring no cluster
    /// update.
    /// </para>
    /// <para>
    /// It's also possible to have multiple overlapping updates.  For example,
    /// you can have updates like <b>1 --> 2</b>, <b>2 --> 3</b>, and
    /// <b>1 --> 3</b>.  The first two updates upgrade the cluster from version
    /// 1 to version 2 in two steps while the second update upgrades the cluster
    /// in just one step.  This is a  nice way to consolidate multiple updates from
    /// a series of test or edge releases so they can be applied one step to
    /// a production cluster.  <b>neon-cli</b> will favor updates that upgrade 
    /// the cluster to the most recent version.
    /// </para>
    /// <para>
    /// The <see cref="AddUpdateSteps(SetupController{NodeDefinition})"/> method adds
    /// the idempotent steps required to upgrade the cluster.
    /// </para>
    /// </remarks>
    public interface IClusterUpdate
    {        
        /// <summary>
        /// Specifies the minimum cluster version to which this update applies.
        /// </summary>
        SemanticVersion FromVersion { get; }

        /// <summary>
        /// Specifies the cluster version after all updates are applied.
        /// </summary>
        SemanticVersion ToVersion { get; }

        /// <summary>
        /// The cluster proxy.
        /// </summary>
        ClusterProxy Cluster { get; set; }

        /// <summary>
        /// Returns the cluster login.
        /// </summary>
        ClusterLogin ClusterLogin { get; }

        /// <summary>
        /// Adds the update steps to a setup controller.
        /// </summary>
        /// <paramref name="controller">The setup controller.</paramref>
        void AddUpdateSteps(SetupController<NodeDefinition> controller);

        /// <summary>
        /// Returns the idempotency tag to use for the named update operation.
        /// </summary>
        /// <param name="operation">The operation name consisting of letters, digits, and dashes.</param>
        /// <returns>The idempotent tag.</returns>
        /// <remarks>
        /// The value returned must be look like <b>update/TO-VERSION/PREFIX</b> where
        /// <b>TO-VERSION</b> is the post update version, and PREFIX is <see cref="IdempotentPrefix"/>.
        /// </remarks>
        string GetIdempotentTag(string operation);

        /// <summary>
        /// Annotates the <paramref name="stepLabel"/> passed by prefixing it with the 
        /// update versions.
        /// </summary>
        /// <param name="stepLabel">The update step label.</param>
        /// <returns>Thr annotated label.</returns>
        string GetStepLabel(string stepLabel);
    }
}
