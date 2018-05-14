//-----------------------------------------------------------------------------
// FILE:	    AnsiblePlayer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;

using Neon.Common;
using Neon.IO;

namespace Neon.Xunit
{
    /// <summary>
    /// Used for running Ansible playbooks within unit tests.
    /// </summary>
    public static class AnsiblePlayer
    {
        /// <summary>
        /// Plays a playbook within a specific working directory using <b>neon ansible play -- [args] playbook</b>.
        /// </summary>/
        /// <param name="workDir">The playbook working directory (or <c>null</c> to use a temporary folder).</param>
        /// <param name="playbook">The playbook text.</param>
        /// <param name="args">Optional command line arguments to be included in the command.</param>
        /// <returns>An <see cref="AnsiblePlayResults"/> describing what happened.</returns>
        /// <remarks>
        /// <note>
        /// Use this method for playbooks that need to read or write files.
        /// </note>
        /// </remarks>
        public static AnsiblePlayResults PlayInFolder(string workDir, string playbook, params string[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(playbook));

            if (!string.IsNullOrEmpty(workDir))
            {
                Directory.CreateDirectory(workDir);

                Environment.CurrentDirectory = workDir;
                File.WriteAllText(Path.Combine(workDir, "play.yaml"), playbook);

                var response = NeonHelper.ExecuteCaptureStreams("neon", new object[] { "ansible", "play", "--noterminal", "--", args, "-vvvv", "play.yaml" });

                return new AnsiblePlayResults(response);
            }
            else
            {
                using (var folder = new TempFolder())
                {
                    var orgDirectory = Environment.CurrentDirectory;

                    try
                    {
                        Environment.CurrentDirectory = folder.Path;
                        File.WriteAllText(Path.Combine(folder.Path, "play.yaml"), playbook);

                        var response = NeonHelper.ExecuteCaptureStreams("neon", new object[] { "ansible", "play", "--noterminal", "--", args, "-vvvv", "play.yaml" });

                        return new AnsiblePlayResults(response);
                    }
                    finally
                    {
                        Environment.CurrentDirectory = orgDirectory;
                    }
                }
            }
        }

        /// <summary>
        /// Plays a playbook within a temporary directory using <b>neon ansible play -- [args] playbook</b>.
        /// </summary>/
        /// <param name="playbook">The playbook text.</param>
        /// <param name="args">Optional command line arguments to be included in the command.</param>
        /// <returns>An <see cref="AnsiblePlayResults"/> describing what happened.</returns>
        /// <remarks>
        /// <note>
        /// Use this method for playbooks that need to read or write files.
        /// </note>
        /// </remarks>
        public static AnsiblePlayResults Play(string playbook, params string[] args)
        {
            return PlayInFolder(null, playbook, args);
        }
    }
}
