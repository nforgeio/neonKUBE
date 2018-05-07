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
        /// <para>
        /// Optionally configures the working directory to use when running playbooks
        /// rather than generating a temporary folder.
        /// </para>
        /// <note>
        /// This can be useful when debugging playbooks in unit tests.  We recommend
        /// that you do not use when actually performing production testing.
        /// </note>
        /// </summary>
        public static string WorkDir { get; set; }

        /// <summary>
        /// Plays the playbook using <b>neon ansible play -- [args] playbook</b>.
        /// </summary>/
        /// <param name="playbook">The playbook text.</param>
        /// <param name="args">Optional command line arguments to be included in the command.</param>
        /// <returns>An <see cref="AnsiblePlayResults"/> describing what happened.</returns>
        public static AnsiblePlayResults NeonPlay(string playbook, params string[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(playbook));

            if (!string.IsNullOrEmpty(WorkDir))
            {
                Directory.CreateDirectory(WorkDir);

                Environment.CurrentDirectory = WorkDir;
                File.WriteAllText(Path.Combine(WorkDir, "play.yaml"), playbook);

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
    }
}
