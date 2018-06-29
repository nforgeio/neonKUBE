//-----------------------------------------------------------------------------
// FILE:	    ConfigStepList.cs
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
    /// Implements a list of <see cref="ConfigStep"/>s to be performed
    /// on a neonHIVE.
    /// </summary>
    public class ConfigStepList : List<ConfigStep>
    {
        /// <summary>
        /// Adds a set of configuration steps to the list.
        /// </summary>
        /// <param name="steps">The steps.</param>
        public void Add(IEnumerable<ConfigStep> steps)
        {
            if (steps == null)
            {
                return;
            }

            foreach (var step in steps)
            {
                base.Add(step);
            }
        }
    }
}
