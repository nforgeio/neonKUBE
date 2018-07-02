//-----------------------------------------------------------------------------
// FILE:	    ActionStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Runs an <see cref="Action{SshProxy}"/> as a hive setup step.
    /// </summary>
    public class ActionStep : ConfigStep
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a configuration step that executes an potentially idempotent <see cref="Action"/>
        /// on a specific Docker node.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="operationName">The idempotent operation name or <c>null</c> if the operation is not idempotent.</param>
        /// <param name="action">The action to be invoked.</param>
        public static ActionStep Create(string nodeName, string operationName, Action<SshProxy<NodeDefinition>> action)
        {
            return new ActionStep(nodeName, operationName, action);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string                              nodeName;
        private string                              operationName;
        private Action<SshProxy<NodeDefinition>>    action;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="operationName">The idempotent operation name or <c>null</c> if the operation is not idempotent.</param>
        /// <param name="action">The action to be invoked.</param>
        private ActionStep(string nodeName, string operationName, Action<SshProxy<NodeDefinition>> action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));
            Covenant.Requires<ArgumentNullException>(action != null);

            this.nodeName      = nodeName;
            this.operationName = operationName;
            this.action        = action;
        }

        /// <inheritdoc/>
        public override void Run(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            var node = hive.GetNode(nodeName);

            if (operationName != null)
            {
                node.InvokeIdempotentAction(operationName, () => action(node));
            }
            else
            {
                action(node);
            }
        }
    }
}
