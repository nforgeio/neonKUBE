//-----------------------------------------------------------------------------
// FILE:	    TlsCertificate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

// $todo(jeff.lill): 
//
// Support construction from an X509Certificate instance once
// .NET Standard 2.0 is released.

namespace Neon.Cryptography
{
    /// <summary>
    /// Holds the public and private parts of a TLS certificate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class follows the <b>HAProxy</b> convention of allowing the PEM encoded public certificate
    /// and private key to be encoded into a single text file by simply concatenating the public
    /// certificate with the private key, certificate first.
    /// </para>
    /// <note>
    /// The certificate part must include any intermediate certificates issues by the certificate
    /// authority after the certificate and before the private key.
    /// </note>
    /// </remarks>
    public class TlsCertificate
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads a combined public certificate and private key from a PEM encoded
        /// text file.
        /// </summary>
        /// <param name="pemCombinedPath">Path to the source file.</param>
        /// <returns>The parsed <see cref="TlsCertificate"/>.</returns>
        public static TlsCertificate Load(string pemCombinedPath)
        {
            return new TlsCertificate(File.ReadAllText(pemCombinedPath));
        }

        /// <summary>
        /// Loads a combined public certificate and private key from two PEM encoded
        /// files.
        /// </summary>
        /// <param name="certPath">Path to the public certificate PEM file.</param>
        /// <param name="keyPath">Path to the private key PEM file.</param>
        /// <returns>The parsed <see cref="TlsCertificate"/>.</returns>
        public static TlsCertificate Load(string certPath, string keyPath)
        {
            return new TlsCertificate(File.ReadAllText(certPath), File.ReadAllText(keyPath));
        }

        /// <summary>
        /// Normalizes PEM encoded text to have Linux style (LF) line endings.
        /// </summary>
        /// <param name="input">The input text.</param>
        /// <returns>The normalized text.</returns>
        public static string NormalizePem(string input)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input));

            var output = NeonHelper.ToLinuxLineEndings(input);

            if (output.EndsWith("\n"))
            {
                return output;
            }
            else
            {
                return output + "\n";
            }
        }

        /// <summary>
        /// Generates a self-signed certificate for a host name.
        /// </summary>
        /// <param name="hostName">The host name.</param>
        /// <param name="bitCount">The certificate key size in bits: one of <b>1024</b>, <b>2048</b>, or <b>4096</b>.</param>
        /// <param name="validDays">
        /// The number of days the certificate will be valid.  This defaults to 365,000 days
        /// or about 1,000 years.
        /// </param>
        /// <returns>The <see cref="TlsCertificate"/>.</returns>
        public static TlsCertificate CreateSelfSigned(string hostName, int bitCount, int validDays = 365000)
        {
            Covenant.Requires<ArgumentException>(!string.IsNullOrEmpty(hostName));
            Covenant.Requires<ArgumentException>(bitCount == 1024 || bitCount == 2048 || bitCount == 4096);
            Covenant.Requires<ArgumentException>(validDays > 1);

            var tempFolder   = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var certPath     = Path.Combine(tempFolder, "cache.crt");
            var keyPath      = Path.Combine(tempFolder, "cache.key");
            var combinedPath = Path.Combine(tempFolder, "combined.pem");

            Directory.CreateDirectory(tempFolder);

            try
            {
                var result = NeonHelper.ExecuteCaptureStreams("openssl",
                    $"req -newkey rsa:{bitCount} -nodes -sha256 -x509 -days {validDays} " +
                    $"-subj \"/C=--/ST=./L=./O=./CN={hostName}\" " +
                    $"-keyout \"{keyPath}\" " +
                    $"-out {certPath}");

                if (result.ExitCode != 0)
                {
                    throw new Exception($"Certificate Error: {result.ErrorText}");
                }

                File.WriteAllText(combinedPath, File.ReadAllText(certPath) + File.ReadAllText(keyPath));

                var certificate = TlsCertificate.Load(combinedPath);

                certificate.Parse();

                return certificate;
            }
            catch (Win32Exception e)
            {
                throw new Exception($"Cannot find the [openssl] utility on the PATH.", e);
            }
            finally
            {
                Directory.Delete(tempFolder, true);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs an uninitialized certificate.
        /// </summary>
        public TlsCertificate()
        {
        }

        /// <summary>
        /// Constructs an instance by parsing the combined site certificate, any intermediate
        /// certificates followed by the private key as PEM encoded text.
        /// </summary>
        /// <param name="pemCombined">The certificate(s) followed by the private key text.</param>
        public TlsCertificate(string pemCombined)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(pemCombined));

            var certPos = pemCombined.IndexOf("-----BEGIN CERTIFICATE-----");
            var keyPos  = pemCombined.IndexOf("-----BEGIN PRIVATE KEY-----");

            if (certPos == -1)
            {
                throw new ArgumentException($"[{nameof(pemCombined)}] does not include a certificate.");
            }

            if (keyPos == -1)
            {
                throw new ArgumentException($"[{nameof(pemCombined)}] does not include a private key.");
            }

            if (keyPos < certPos)
            {
                throw new ArgumentException($"[{nameof(pemCombined)}] is improperly formatted: The private key must be after the certificate.");
            }

            if (certPos > 0)
            {
                throw new ArgumentException($"[{nameof(pemCombined)}] is improperly formatted: There is extra text before the certificate.");
            }

            Cert = NormalizePem(pemCombined.Substring(0, keyPos));
            Key  = NormalizePem(pemCombined.Substring(keyPos));
        }

        /// <summary>
        /// Constructs an instance by parsing the certificate and private key PEM encoded
        /// text passed.
        /// </summary>
        /// <param name="certPem">The certificate PEM text.</param>
        /// <param name="keyPem">The private key PEM text.</param>
        public TlsCertificate(string certPem, string keyPem)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(certPem));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(keyPem));

            if (!certPem.StartsWith("-----BEGIN CERTIFICATE-----"))
            {
                throw new ArgumentException($"[{nameof(certPem)}] is improperly formatted.");
            }

            if (!keyPem.StartsWith("-----BEGIN PRIVATE KEY-----"))
            {
                throw new ArgumentException($"[{nameof(keyPem)}] is improperly formatted.");
            }

            Cert = NormalizePem(certPem);
            Key  = NormalizePem(keyPem);
        }

        /// <summary>
        /// The public certificate as PEM encoded text.
        /// </summary>
        [JsonProperty(PropertyName = "Cert", Required = Required.Always | Required.DisallowNull)]
        public string Cert { get; set; }

        /// <summary>
        /// The private key as PEM encoded text.
        /// </summary>
        [JsonProperty(PropertyName = "Key", Required = Required.Always | Required.DisallowNull)]
        public string Key { get; set; }

        /// <summary>
        /// The date when the certificate becomes valid (or <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "ValidFrom", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DateTime? ValidFrom { get; set; }

        /// <summary>
        /// The date when the certificate expires (or <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "ValidUntil", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DateTime? ValidUntil { get; set; }

        /// <summary>
        /// The DNS hostnames covered by the certificate.  This may be a single or wildcard hostname
        /// extracted from the the certificate's <b>Common Name</b> or multiple hostname extracted
        /// from the <b>Subject Alternative Name</b> from a SAN certificate.  This list will be 
        /// <c>null</c> or empty if the hostname(s) are unknown.
        /// </summary>
        [JsonProperty(PropertyName = "Hosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Hosts { get; set; } = new List<string>();

        /// <summary>
        /// Returns a deep copy of the instance.
        /// </summary>
        /// <returns>The clone.</returns>
        public TlsCertificate Clone()
        {
            var clone =
                new TlsCertificate()
                {
                    Cert       = this.Cert,
                    Key        = this.Key,
                    ValidFrom  = this.ValidFrom,
                    ValidUntil = this.ValidUntil
                };

            foreach (var host in this.Hosts)
            {
                clone.Hosts.Add(host);
            }

            return clone;
        }

        /// <summary>
        /// Returns the DNS hostnames covered by the certificate as a comma separated string.
        /// </summary>
        [JsonIgnore]
        public string HostNames
        {
            get
            {
                var sb = new StringBuilder();

                foreach (var host in Hosts)
                {
                    sb.AppendWithSeparator(host, ",");
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Combines the public certificate and private key into a PEM encoded certificate
        /// compatible with HAProxy.
        /// </summary>
        /// <returns>The combined PEM coded certificiate.</returns>
        public string Combine()
        {
            return NormalizePem(Cert) + NormalizePem(Key);
        }

        /// <summary>
        /// Determines whether the certificate is valid for the current or an optionally a specified date.
        /// </summary>
        /// <param name="dateTimeUtc">The optional date (UTC) to check.</param>
        /// <returns><c>true</c> if the certificate is valid for the date.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if either of <see cref="ValidFrom"/> or <see cref="ValidUntil"/> are <c>null</c>.
        /// </exception>
        public bool IsValidDate(DateTime? dateTimeUtc = null)
        {
            if (ValidFrom == null || ValidUntil == null)
            {
                throw new InvalidOperationException($"Cannot verify certificate validity when either of the [{nameof(ValidFrom)}] or [{nameof(ValidUntil)}] fields are NULL.");
            }

            dateTimeUtc = dateTimeUtc ?? DateTime.UtcNow;

            return ValidFrom <= dateTimeUtc && dateTimeUtc <= ValidUntil;
        }

        /// <summary>
        /// Determines whether the certificate is valid for a hostname.
        /// </summary>
        /// <param name="hostName">The hostname to validate.</param>
        /// <returns><c>true</c> if the certificate is valid for the hostname.</returns>
        /// <exception cref="InvalidOperationException"> Thrown if <see cref="Hosts"/> is <c>null</c> or empty.</exception>
        public bool IsValidHost(string hostName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostName));

            if (ValidFrom == null || ValidUntil == null)
            {
                throw new InvalidOperationException($"Cannot verify certificate validity when either of the [{nameof(ValidFrom)}] or [{nameof(ValidUntil)}] fields are NULL.");
            }

            if (Hosts == null || Hosts.Count == 0)
            {
                throw new InvalidOperationException($"Cannot verify certificate validity when [{nameof(Hosts)}] is NULL or empty.");
            }

            foreach (var certHost in Hosts)
            {
                if (certHost.StartsWith("*."))
                {
                    // Wildcard:
                    //
                    // We're going to strip off the leading segment off of both certificate and
                    // test hostname up to the first dot and then compare what's left.

                    var certHostDotPos = certHost.IndexOf('.');
                    var hostDotPos     = hostName.IndexOf('.');

                    if (certHostDotPos == -1 || hostDotPos == -1)
                    {
                        throw new FormatException("Misformatted hostname or certificate host.");
                    }

                    if (string.Equals(certHost.Substring(certHostDotPos), hostName.Substring(hostDotPos), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;    // We have a match
                    }
                }
                else
                {
                    if (string.Equals(certHost, hostName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;    // Wen have a match
                    }
                }
            }

            // The hostname specfied didn't match any of the certificate hosts.

            return false;
        }

        /// <summary>
        /// Extracts a field from a certificate dump.
        /// </summary>
        /// <param name="prefix">The field prefix.</param>
        /// <param name="info">The certificate dump.</param>
        /// <param name="throwOnNotFound">Can optionally disable not found error checking.</param>
        /// <returns>The extracted field or <c>null</c> if it was not found.</returns>
        /// <exception cref="FormatException">Thrown if the field wasn't found and error checking isn't disabled.</exception>
        private string ExtractField(string prefix, string info, bool throwOnNotFound = true)
        {
            var pos = info.IndexOf(prefix);

            if (pos == -1)
            {
                if (throwOnNotFound)
                {
                    throw new FormatException($"Certificate dump does not include the [{prefix}] field.");
                }
                else
                {
                    return null;
                }
            }

            pos += prefix.Length;

            // Extract up the the end of the line.

            var posEol = info.IndexOf('\n', pos);

            if (posEol == -1)
            {
                return info.Substring(pos).Trim();
            }
            else
            {
                return info.Substring(pos, posEol - pos).Trim();
            }
        }

        /// <summary>
        /// Extracts certificate properties such as <see cref="ValidFrom"/>, <see cref="ValidUntil"/>, and <see cref="Hosts"/> 
        /// from the dump output from the Windows <b>CertUtil.exe</b> tool (e.g. via <c>certutil -dump cert.pem</c>).
        /// </summary>
        /// <param name="info">The dumped certificate information.</param>
        /// <remarks>
        /// <note>
        /// Interesting Fact: <b>OpenSSL</b> and <b>CertUtil</b> report slightly different valid dates
        /// for the same certificate.  It appears that <b>CertUtil</b> reports the actual dates whereas
        /// <b>OpenSSL</b> rounds <see cref="ValidFrom"/> down to <b>00:00:00</b> and rounds <see cref="ValidUntil"/> 
        /// up to <b>23:59:59</b>.  I'm guessing OpenSSL is doing this to give clients that do not
        /// properly handle UTC conversions times some leaway at the beginning and end of a certficiate's life.
        /// </note>
        /// </remarks>
        public void ParseCertUtil(string info)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(info));

            const string datePattern = "M/d/yyyy h:mm tt";

            var notBefore = ExtractField("NotBefore:", info);
            var notAfter  = ExtractField("NotAfter:", info);

            var date = DateTime.ParseExact(notBefore, datePattern, CultureInfo.InvariantCulture);

            ValidFrom = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Utc);

            date = DateTime.ParseExact(notAfter, datePattern, CultureInfo.InvariantCulture);

            ValidUntil = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Utc);

            using (var reader = new StringReader(info))
            {
                var posSAN = info.IndexOf("Subject Alternative Name");

                if (posSAN != -1)
                {
                    // This is a SAN certificate so we'll extract the [DNS Name=*] lines.

                    // Skip lines up to the [Subject Alternative Name]

                    while (true)
                    {
                        var line = reader.ReadLine();

                        if (line.TrimStart().StartsWith("Subject Alternative Name"))
                        {
                            break;
                        }
                    }

                    // Read the following [DNS Name=] lines.

                    while (true)
                    {
                        var line = reader.ReadLine();

                        if (line == null || !line.TrimStart().StartsWith("DNS Name="))
                        {
                            break;
                        }

                        Hosts.Add(line.Substring(line.IndexOf('=') + 1));
                    }
                }
                else
                {
                    // This is a single host certificate, so we'll extract the [Subject/CN=*] value.

                    // Skip lines up to the [Subject:]

                    while (true)
                    {
                        var line = reader.ReadLine();

                        if (line.TrimStart().StartsWith("Subject:"))
                        {
                            break;
                        }
                    }

                    // Read the following lines until we see one that starts with [CN=]

                    while (true)
                    {
                        var line = reader.ReadLine();

                        if (line == null)
                        {
                            throw new FormatException("Invalid certificate dump: Subject/CN not found.");
                        }

                        if (line.TrimStart().StartsWith("CN="))
                        {
                            Hosts.Add(line.Substring(line.IndexOf('=') + 1));
                            break;
                        }
                    }
                }
            }

            if (Hosts.Count == 0)
            {
                throw new FormatException("Invalid certificate dump: No host names found.");
            }
        }

        /// <summary>
        /// Extracts certificate properties such as <see cref="ValidFrom"/>, <see cref="ValidUntil"/>, and <see cref="Hosts"/> 
        /// from the dump output from the <b>OpenSSL</b> tool (e.g. via <c>openssl -in cert.pem -text</c>).
        /// </summary>
        /// <param name="info">The dumped certificate information.</param>
        /// <remarks>
        /// <note>
        /// Interesting Fact: <b>OpenSSL</b> and <b>CertUtil</b> report slightly different valid dates
        /// for the same certificate.  It appears that <b>CertUtil</b> reports the actual dates whereas
        /// <b>OpenSSL</b> rounds <see cref="ValidFrom"/> down to <b>00:00:00</b> and rounds <see cref="ValidUntil"/> 
        /// up to <b>23:59:59</b>.  I'm guessing OpenSSL is doing this to give clients that do not
        /// properly handle UTC conversions times some leaway at the beginning and end of a certficiate's life.
        /// </note>
        /// </remarks>
        public void ParseOpenSsl(string info)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(info));

            const string datePattern = "MMM d HH:mm:ss yyyy";

            var notBefore = ExtractField("Not Before:", info).Replace(" GMT", string.Empty);
            var notAfter  = ExtractField("Not After :", info).Replace(" GMT", string.Empty);

            // $hack(jeff.lill):
            //
            // OpenSSH on Linux includes an extra space for single digit day fields
            // like: "Feb  4 01:45:25 2117".  This causes ParseExact() to fail.  We're
            // going to work around this by converting the two spaces into a single space.

            notBefore = notBefore.Replace("  ", " ");
            notAfter  = notAfter.Replace("  ", " ");

            var date = DateTime.ParseExact(notBefore, datePattern, CultureInfo.InvariantCulture);

            ValidFrom = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Utc);

            date = DateTime.ParseExact(notAfter, datePattern, CultureInfo.InvariantCulture);

            ValidUntil = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Utc);

            using (var reader = new StringReader(info))
            {
                var posSAN = info.IndexOf("X509v3 Subject Alternative Name:");

                if (posSAN != -1)
                {
                    // This is a SAN certificate so we'll extract the comma separated [DNS:*] entries.

                    // Skip lines up to the [X509v3 Subject Alternative Name:]

                    while (true)
                    {
                        var line = reader.ReadLine();

                        if (line.TrimStart().StartsWith("X509v3 Subject Alternative Name:"))
                        {
                            break;
                        }
                    }

                    // The next line specifies the comma separated DNS host names.

                    var hostEntries = reader.ReadLine().Split(',');

                    foreach (var entry in hostEntries)
                    {
                        Hosts.Add(entry.Replace("DNS:", string.Empty).Trim());
                    }
                }
                else
                {
                    // This is a single host certificate, so we'll extract the [Subject/CN=*] value.

                    var subject = ExtractField("Subject:", info);
                    var fields  = subject.Split(',');

                    foreach (var field in fields)
                    {
                        var trimmed = field.Trim();

                        if (trimmed.StartsWith("CN="))
                        {
                            Hosts.Add(trimmed.Substring(3).Trim());
                            break;
                        }
                        else if (trimmed.StartsWith("CN = "))
                        {
                            Hosts.Add(trimmed.Substring(5).Trim());
                            break;
                        }
                    }
                }
            }

            if (Hosts.Count == 0)
            {
                throw new FormatException("Invalid certificate dump: No host names found.");
            }
        }

        /// <summary>
        /// Attempts to parse the certificate details.
        /// </summary>
        public void Parse()
        {
            // $todo(jeff.lill):
            //
            // Hacking this using the [CertUtil] and [OpenSSL] tools until the X509Certificate
            // class is implemented in .NET Standard 2.0.

            var tempCertPath = Path.GetTempFileName();
            var tool         = "openssl";

            try
            {
                File.WriteAllText(tempCertPath, this.Combine());

                var sbArgs = new StringBuilder();

                // We're going to use [CertUtil] for Windows and [OpenSSL]
                // for everything else.

                if (NeonHelper.IsWindows)
                {
                    tool = "certutil";

                    sbArgs.Append("-dump ");
                    sbArgs.Append($"\"{tempCertPath}\" ");

                    var result = NeonHelper.ExecuteCaptureStreams("certutil", sbArgs.ToString());

                    if (result.ExitCode != 0)
                    {
                        throw new FormatException("Cannot parse certificate.");
                    }

                    ParseCertUtil(result.OutputText);
                }
                else
                {
                    sbArgs.Append($"x509 -in \"{tempCertPath}\" ");
                    sbArgs.Append("-text");

                    var result = NeonHelper.ExecuteCaptureStreams("openssl", sbArgs.ToString());

                    if (result.ExitCode != 0)
                    {
                        throw new FormatException("Cannot parse certificate.");
                    }

                    ParseOpenSsl(result.OutputText);
                }
            }
            catch (Win32Exception)
            {
                throw new Exception($"Cannot find or execute the [{tool}] SSL certificate utility on the PATH.");
            }
            finally
            {
                File.Delete(tempCertPath);
            }
        }
    }
}