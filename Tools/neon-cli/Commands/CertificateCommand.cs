//-----------------------------------------------------------------------------
// FILE:	    CertificateCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>certificate</b>|<b>cert</b> command.
    /// </summary>
    public class CertificateCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds information about a listed certificate.
        /// </summary>
        private class CertInfo
        {
            public string Name { get; set; }
            public string ValidUntil { get; set; }
            public string Hosts { get; set; }

            public CertInfo(string name, TlsCertificate cert)
            {
                this.Name = name;

                if (cert.ValidUntil.HasValue)
                {
                    this.ValidUntil = cert.ValidUntil.Value.ToString("MM/dd/yyy HH:mm:ss", CultureInfo.InvariantCulture) + " GMT";
                }
                else
                {
                    this.ValidUntil = "-";
                }

                if (cert.Hosts != null && cert.Hosts.Count > 0)
                {
                    var sb = new StringBuilder();

                    foreach (var host in cert.Hosts)
                    {
                        sb.AppendWithSeparator(host, ", ");
                    }

                    this.Hosts = sb.ToString();
                }
                else
                {
                    this.Hosts = "";
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string usage = @"
Manages hive TLS certificiates.

USAGE:

    neon certificate|cert combine PATH-CERT PATH-KEY PATH-OUTPUT
    neon certificate|cert get NAME
    neon certificate|cert list|ls [--expired | --expiring]
    neon certificate|cert remove|rm NAME
    neon certificate|cert set NAME PATH
    neon certificate|cert split PATH PATH-CERT PATH-KEY 
    neon certificate|cert verify PATH

ARGUMENTS:

    NAME        - Identifies the certificate in the hive
    PATH        - Local file system path
    PATH-CERT   - Certificate path (without the private key)
    PATH-KEY    = Private key path

DETAILS:

neonHIVE standardizes on HAProxy compatible PEM-encoded certificates.
These include both the public certificate and private key into a single
file.  The certificate appears first, followed by any intermediate
certificates, and then finally the private key.

    get         Retrieves a named hive certificate.
    join        Combines a certificate and key into a single file.

    list|ls     Lists the hive certificates by name including
                the expiration dates and covered hosts.

                --expired   lists ceritficates that have expired.
                --expiring  lists certificates that have expired
                            or will expire within 30 days.

    remove|rm   Removes a named hive certificate.
    set         Saves or updates a named hive certificate.
    split       Splits a certificate into its parts.
    verify      Verifies a local certificate file.

";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cert" }; }
        }

        /// <inheritdoc/>
        public override string[] AltWords
        {
            get { return new string[] { "certificate" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--expired", "--expiring" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            // Process the command arguments.

            TlsCertificate  certificate;
            string          certName;

            var command = commandLine.Arguments.FirstOrDefault();

            if (command == null)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            commandLine = commandLine.Shift(1);

            switch (command)
            {
                case "get":

                    Program.ConnectHive();

                    certName = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(certName))
                    {
                        Console.Error.WriteLine("*** ERROR: Expected arguments: NAME");
                        Program.Exit(1);
                    }

                    if (!HiveDefinition.IsValidName(certName))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{certName}] is not a valid certificate name.");
                        Program.Exit(1);
                    }

                    certificate = HiveHelper.Hive.Certificate.Get(certName);

                    if (certificate == null)
                    {
                        Console.Error.WriteLine($"*** ERROR: Certificate [{certName}] does not exist.");
                        Program.Exit(1);
                    }

                    Console.WriteLine(certificate.CombinedPem);
                    break;

                case "join":

                    if (commandLine.Arguments.Length != 3)
                    {
                        Console.Error.WriteLine("*** ERROR: Expected arguments: PATH-CERT PATH-KEY PATH-OUTPUT");
                        Program.Exit(1);
                    }

                    certificate = TlsCertificate.Load(commandLine.Arguments[0], commandLine.Arguments[1]);

                    File.WriteAllText(commandLine.Arguments[2], certificate.CombinedPem);
                    break;

                case "list":
                case "ls":

                    Program.ConnectHive();

                    var certList = new List<CertInfo>();

                    DateTime?   checkDate = null;
                    bool        expired   = false;

                    if (commandLine.GetOption("--expired") != null)
                    {
                        checkDate = DateTime.UtcNow;
                        expired   = true;
                    }
                    else if (commandLine.GetOption("--expiring") != null)
                    {
                        checkDate = DateTime.UtcNow + TimeSpan.FromDays(30);
                    }

                    // List the certificate key/names and then fetch each one
                    // to capture details like the expiration date and covered
                    // hostnames.

                    foreach (var name in HiveHelper.Hive.Certificate.List())
                    {
                        certificate = HiveHelper.Hive.Certificate.Get(name);

                        if (checkDate.HasValue && certificate.IsValidDate(checkDate))
                        {
                            continue;
                        }

                        certList.Add(new CertInfo(name, certificate));
                    }

                    if (checkDate.HasValue && certList.Count == 0)
                    {
                        Console.WriteLine(expired ? "* No certificates have expired." : "* No certificates are expiring within 30 days.");
                        Program.Exit(0);
                    }

                    if (certList.Count > 0)
                    {
                        var nameHeader       = "Name";
                        var validUntilHeader = "Valid Until";
                        var hostsHeader      = "Hosts";
                        var nameColumnWidth  = Math.Max(nameHeader.Length, certList.Max(ci => ci.Name.Length));
                        var dateColumnWidth  = Math.Max(validUntilHeader.Length, certList.Max(ci => ci.ValidUntil.Length));
                        var hostColumnWidth  = Math.Max(hostsHeader.Length, certList.Max(ci => ci.Hosts.Length));

                        Console.WriteLine($"{nameHeader}{new string(' ', nameColumnWidth - "Name".Length)}   {validUntilHeader}{new string(' ', dateColumnWidth - validUntilHeader.Length)}   {hostsHeader}");
                        Console.WriteLine($"{new string('-', nameColumnWidth)}   {new string('-', dateColumnWidth)}   {new string('-', hostColumnWidth)}");

                        foreach (var certInfo in certList.OrderBy(ci => ci.Name.ToLowerInvariant()))
                        {
                            Console.WriteLine($"{certInfo.Name}{new string(' ', nameColumnWidth - certInfo.Name.Length)}   {certInfo.ValidUntil}{new string(' ', dateColumnWidth - certInfo.ValidUntil.Length)}   {certInfo.Hosts}");
                        }

                        if (checkDate.HasValue)
                        {
                            Program.Exit(1);
                        }
                    }
                    else
                    {
                        Console.WriteLine("* No certificates");
                    }
                    break;

                case "remove":
                case "rm":

                    Program.ConnectHive();

                    certName = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(certName))
                    {
                        Console.Error.WriteLine("*** ERROR: Expected arguments: NAME");
                        Program.Exit(1);
                    }

                    if (!HiveDefinition.IsValidName(certName))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{certName}] is not a valid certificate name.");
                        Program.Exit(1);
                    }

                    if (HiveHelper.Hive.Certificate.Get(certName) != null)
                    {
                        HiveHelper.Hive.Certificate.Remove(certName);
                        Console.WriteLine($"Certificate [{certName}] was removed.");
                    }
                    else
                    {
                        Console.Error.WriteLine($"*** ERROR: [{certName}] does not exist.");
                        Program.Exit(1);
                    }
                    break;

                case "set":

                    Program.ConnectHive();

                    using (var vault = HiveHelper.OpenVault(Program.HiveLogin.VaultCredentials.RootToken))
                    {
                        if (commandLine.Arguments.Length != 2)
                        {
                            Console.Error.WriteLine("*** ERROR: Expected arguments: NAME PATH");
                            Program.Exit(1);
                        }

                        certName = commandLine.Arguments.FirstOrDefault();

                        if (string.IsNullOrEmpty(certName))
                        {
                            Console.Error.WriteLine("*** ERROR: Expected arguments: NAME");
                            Program.Exit(1);
                        }

                        if (!HiveDefinition.IsValidName(certName))
                        {
                            Console.Error.WriteLine($"*** ERROR: [{certName}] is not a valid certificate name.");
                            Program.Exit(1);
                        }

                        certificate = TlsCertificate.Load(commandLine.Arguments.ElementAtOrDefault(1));

                        certificate.Parse();

                        if (HiveHelper.Hive.Certificate.Get(certName) == null)
                        {
                            HiveHelper.Hive.Certificate.Set(certName, certificate);
                            Console.WriteLine($"Certificate [{certName}] was added.");
                        }
                        else
                        {
                            HiveHelper.Hive.Certificate.Set(certName, certificate);
                            Console.WriteLine($"Certificate [{certName}] was updated.");
                        }
                    }
                    break;

                case "split":

                    if (commandLine.Arguments.Length != 3)
                    {
                        Console.Error.WriteLine("*** ERROR: Expected arguments: PATH PATH-CERT PATH-KEY");
                        Program.Exit(1);
                    }

                    certificate = TlsCertificate.Load(commandLine.Arguments[0]);

                    File.WriteAllText(commandLine.Arguments[1], certificate.CertPem);
                    File.WriteAllText(commandLine.Arguments[2], certificate.KeyPem);
                    break;

                case "verify":

                    VerifyLocalCertificate(commandLine);
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unknown command: [{command}]");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            var commandLine = shim.CommandLine;
            var command     = commandLine.Arguments.ElementAtOrDefault(1);

            switch (command)
            {
                // These commands should be run on the operator workstation.

                case "join":
                case "split":

                    return new DockerShimInfo(shimability: DockerShimability.None);

                // These commands can be run in Docker without modification.

                case "set":

                    if (commandLine.Arguments.Length >= 4)
                    {
                        shim.AddFile(commandLine.Arguments[3]);
                    }

                    return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);

                case "verify":

                    foreach (var path in commandLine.Arguments.Skip(2))
                    {
                        shim.AddFile(path);
                    }

                    return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);

                case "remove":
                case "rm":
                case "get":
                case "ls":
                case "list":
                default:

                    return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
            }
        }

        /// <summary>
        /// Verifies a local certificate file.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void VerifyLocalCertificate(CommandLine commandLine)
        {
            // $todo(jeff.lill):
            //
            // Consider using the new [TlsCertificate.Validate()] method.  The only
            // disadvantage of this method is that it doesn't output the error
            // messages from the certificate tool.
            //
            // On the other hand, this code might become moot when we upgrade
            // to use the built-in .NET X.509 classes.

            if (commandLine.Arguments.Length != 1)
            {
                Console.Error.WriteLine("*** ERROR: Expected argument: PATH");
                Program.Exit(1);
            }

            var certificate = TlsCertificate.Load(commandLine.Arguments[0]);

            // We're going to split the certificate into two files, the issued
            // certificate and the certificate authority's certificate chain
            // (AKA the CA bundle).

            var tempCertPath = Path.GetTempFileName();
            var tempCaPath   = Path.GetTempFileName();
            var tool         = "openssl";

            if (NeonHelper.IsLinux && HiveHelper.InToolContainer)
            {
                // Choose nicer looking temporary file names when we're running the
                // tool container and don't need to worry about conflicting files.

                tempCertPath = Path.GetFileNameWithoutExtension(commandLine.Arguments[0]);
                tempCaPath   = tempCertPath + ".ca";
            }

            try
            {
                var pos = certificate.CertPem.IndexOf("-----END CERTIFICATE-----");

                if (pos == -1)
                {
                    throw new ArgumentNullException("The certificate is not formatted properly.");
                }

                pos = certificate.CertPem.IndexOf("-----BEGIN CERTIFICATE-----", pos);

                var issuedCert = certificate.CertPem.Substring(0, pos);
                var caBundle   = certificate.CertPem.Substring(pos);

                File.WriteAllText(tempCertPath, issuedCert);
                File.WriteAllText(tempCaPath, caBundle);

                var sbArgs = new StringBuilder();

                // We're going to use [certutil] for Windows and [OpenSSL]
                // for everything else.

                if (NeonHelper.IsWindows)
                {
                    tool = "certutil";

                    sbArgs.Append("-verify ");
                    sbArgs.Append($"\"{tempCertPath}\" ");
                    sbArgs.Append($"\"{tempCaPath}\"");

                    var result = NeonHelper.ExecuteCapture("certutil", sbArgs.ToString());

                    Console.WriteLine(result.OutputText);
                    Console.Error.WriteLine(result.ErrorText);

                    Program.Exit(result.ExitCode);
                }
                else
                {
                    sbArgs.Append("verify ");
                    sbArgs.Append("-purpose sslserver ");
                    sbArgs.Append($"-CAfile \"{tempCaPath}\" ");
                    sbArgs.Append($"\"{tempCertPath}\"");

                    var result = NeonHelper.ExecuteCapture("openssl", sbArgs.ToString());

                    Console.WriteLine(result.OutputText);
                    Console.Error.WriteLine(result.ErrorText);

                    Program.Exit(result.ExitCode);
                }
            }
            catch (Win32Exception)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot find the [{tool}] SSL certificate utility on the PATH.");
                Program.Exit(1);
            }
            finally
            {
                File.Delete(tempCertPath);
                File.Delete(tempCaPath);
            }
        }
    }
}
