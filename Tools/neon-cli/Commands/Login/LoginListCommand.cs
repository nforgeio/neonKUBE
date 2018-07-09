//-----------------------------------------------------------------------------
// FILE:	    LoginListCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>logins list</b> command.
    /// </summary>
    public class LoginListCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        private class LoginInfo
        {
            public LoginInfo(HiveLogin hiveLogin, bool viaVpn)
            {
                Name   = hiveLogin.LoginName;
                ViaVpn = viaVpn;

                var info = string.Empty;

                if (hiveLogin.IsRoot)
                {
                    if (info.Length > 0)
                    {
                        info += ", ";
                    }

                    info += "root";
                }

                if (hiveLogin.SetupPending)
                {
                    if (info.Length > 0)
                    {
                        info += ", ";
                    }

                    info += "setup";
                }

                if (ViaVpn)
                {
                    if (info.Length > 0)
                    {
                        info += ", ";
                    }

                    info += "via VPN";
                }

                if (info.Length > 0)
                {
                    this.Info = $"[{info}]";
                }
                else
                {
                    this.Info = string.Empty;
                }
            }

            public string Name;
            public string Info;
            public bool ViaVpn;
        }

        //---------------------------------------------------------------------
        // Implementation
        
        private const string usage = @"
Lists the hive logins available on the local computer.

USAGE:

    neon login list
    neon login ls

";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "login", "list" }; }
        }

        /// <inheritdoc/>
        public override string[] AltWords
        {
            get { return new string[] { "login", "ls" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            var current = CurrentHiveLogin.Load();
            var logins  = new List<LoginInfo>();

            foreach (var file in Directory.EnumerateFiles(Program.HiveLoginFolder, "*.login.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var login  = NeonHelper.JsonDeserialize<HiveLogin>(File.ReadAllText(file));
                    var useVpn = false;

                    if (current != null && string.Equals(current.Login, login.LoginName, StringComparison.OrdinalIgnoreCase))
                    {
                        useVpn = current.ViaVpn;
                    }

                    logins.Add(new LoginInfo(login, useVpn));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"*** ERROR: Cannot read [{file}].  Details: {NeonHelper.ExceptionError(e)}");
                    Program.Exit(1);
                }
            }

            Console.WriteLine();

            if (logins.Count == 0)
            {
                Console.Error.WriteLine("*** No hive logins");
            }
            else
            {
                var maxLoginNameWidth = logins.Max(l => l.Name.Length);

                foreach (var login in logins
                    .OrderBy(c => c.Name.Split('@')[1].ToLowerInvariant())
                    .ThenBy(c => c.Name.Split('@')[0].ToLowerInvariant()))
                {
                    if (current != null && string.Equals(current.Login, login.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Write(" --> ");
                    }
                    else
                    {
                        Console.Write("     ");
                    }

                    var padding = new string(' ', maxLoginNameWidth - login.Name.Length);

                    Console.Write($"{login.Name}{padding}    {login.Info}");

                    Console.WriteLine();
                }
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
