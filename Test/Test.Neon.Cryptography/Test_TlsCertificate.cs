//-----------------------------------------------------------------------------
// FILE:        Test_TlsCertificate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Ignore deprecated [TlsCertificate] warnings.

#pragma warning disable CS0618

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCryptography
{
    public class Test_TlsCertificate
    {
        private const string TestCertPart =
@"-----BEGIN CERTIFICATE-----
MIIFUjCCBDqgAwIBAgIRAPfDO27tYaNpXBph5359NJ8wDQYJKoZIhvcNAQELBQAw
gZAxCzAJBgNVBAYTAkdCMRswGQYDVQQIExJHcmVhdGVyIE1hbmNoZXN0ZXIxEDAO
BgNVBAcTB1NhbGZvcmQxGjAYBgNVBAoTEUNPTU9ETyBDQSBMaW1pdGVkMTYwNAYD
VQQDEy1DT01PRE8gUlNBIERvbWFpbiBWYWxpZGF0aW9uIFNlY3VyZSBTZXJ2ZXIg
Q0EwHhcNMTYxMDE2MDAwMDAwWhcNMTcxMDE2MjM1OTU5WjBbMSEwHwYDVQQLExhE
b21haW4gQ29udHJvbCBWYWxpZGF0ZWQxHTAbBgNVBAsTFFBvc2l0aXZlU1NMIFdp
bGRjYXJkMRcwFQYDVQQDDA4qLm5lb250ZXN0LmNvbTCCASIwDQYJKoZIhvcNAQEB
BQADggEPADCCAQoCggEBAONAsYpPzlBxXQCP57LwUiIte/SXAebVzzcvYqgbr4fK
Jtaag/khJS1O+PeFe2UGGxfeU+dPd7GscdVJfpv4Qkg6g687A4fIxtEu+Mv6W9Wf
82i2xIeCn5zjt8N7ccu8+QAbDX6yrnpQj8sMAeVrcqPcCKHzU4iEklw7iCgg3jki
rG5Tmc9D3SDuLhwC9EIThHUDFwy/RllEcKz6Pi3ZykduqKITcl7V/UtgmScBNaMa
cJqdSLuJFAvtp96QkCXbMYEzlsV/erZh2yKOk12g6QKp8wVyP3nt+mnDqeXvXH/b
NqrftnYW/LbyC7jLIY7mAIU12H4Bwft4tbpOkU7dn08CAwEAAaOCAdkwggHVMB8G
A1UdIwQYMBaAFJCvajqUWgvYkOoSVnPfQ7Q6KNrnMB0GA1UdDgQWBBRwrDYfjjQz
SkGVe9XvPdiYbNTI2TAOBgNVHQ8BAf8EBAMCBaAwDAYDVR0TAQH/BAIwADAdBgNV
HSUEFjAUBggrBgEFBQcDAQYIKwYBBQUHAwIwTwYDVR0gBEgwRjA6BgsrBgEEAbIx
AQICBzArMCkGCCsGAQUFBwIBFh1odHRwczovL3NlY3VyZS5jb21vZG8uY29tL0NQ
UzAIBgZngQwBAgEwVAYDVR0fBE0wSzBJoEegRYZDaHR0cDovL2NybC5jb21vZG9j
YS5jb20vQ09NT0RPUlNBRG9tYWluVmFsaWRhdGlvblNlY3VyZVNlcnZlckNBLmNy
bDCBhQYIKwYBBQUHAQEEeTB3ME8GCCsGAQUFBzAChkNodHRwOi8vY3J0LmNvbW9k
b2NhLmNvbS9DT01PRE9SU0FEb21haW5WYWxpZGF0aW9uU2VjdXJlU2VydmVyQ0Eu
Y3J0MCQGCCsGAQUFBzABhhhodHRwOi8vb2NzcC5jb21vZG9jYS5jb20wJwYDVR0R
BCAwHoIOKi5uZW9udGVzdC5jb22CDG5lb250ZXN0LmNvbTANBgkqhkiG9w0BAQsF
AAOCAQEASaU1ZkviLUXEa2+RoB68UtUIpoid5Gc+d8qQO7b8tHwrv5bYnCUUZ0+9
sPgV4Fd4rgUdSuxog6X3u2OiSs2CUK1gkkXgD/Ag0L+o8u4g3dbSVAS4S5cv0vAR
KWMnS61LDAYdk7sC6YYKUUK0Vg/GVqx0JUVF1TzuVyHvz6h+13ge9SJ02bRUl+2K
W3CUa6mcw1QJEC0gIX2YO4rGht/V4wpr7onxRALyO815lwTqTHcZxSJm3VcHlxrD
g8mENA/ZE594tSlGD9gMv4sQJWPy7X5HuKmVp8jNPUJ6xmkMHBSSV69ZP3WFShRq
ILBSnE7GA4ectcVZSL48xzheonKFGw==
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIIGCDCCA/CgAwIBAgIQKy5u6tl1NmwUim7bo3yMBzANBgkqhkiG9w0BAQwFADCB
hTELMAkGA1UEBhMCR0IxGzAZBgNVBAgTEkdyZWF0ZXIgTWFuY2hlc3RlcjEQMA4G
A1UEBxMHU2FsZm9yZDEaMBgGA1UEChMRQ09NT0RPIENBIExpbWl0ZWQxKzApBgNV
BAMTIkNPTU9ETyBSU0EgQ2VydGlmaWNhdGlvbiBBdXRob3JpdHkwHhcNMTQwMjEy
MDAwMDAwWhcNMjkwMjExMjM1OTU5WjCBkDELMAkGA1UEBhMCR0IxGzAZBgNVBAgT
EkdyZWF0ZXIgTWFuY2hlc3RlcjEQMA4GA1UEBxMHU2FsZm9yZDEaMBgGA1UEChMR
Q09NT0RPIENBIExpbWl0ZWQxNjA0BgNVBAMTLUNPTU9ETyBSU0EgRG9tYWluIFZh
bGlkYXRpb24gU2VjdXJlIFNlcnZlciBDQTCCASIwDQYJKoZIhvcNAQEBBQADggEP
ADCCAQoCggEBAI7CAhnhoFmk6zg1jSz9AdDTScBkxwtiBUUWOqigwAwCfx3M28Sh
bXcDow+G+eMGnD4LgYqbSRutA776S9uMIO3Vzl5ljj4Nr0zCsLdFXlIvNN5IJGS0
Qa4Al/e+Z96e0HqnU4A7fK31llVvl0cKfIWLIpeNs4TgllfQcBhglo/uLQeTnaG6
ytHNe+nEKpooIZFNb5JPJaXyejXdJtxGpdCsWTWM/06RQ1A/WZMebFEh7lgUq/51
UHg+TLAchhP6a5i84DuUHoVS3AOTJBhuyydRReZw3iVDpA3hSqXttn7IzW3uLh0n
c13cRTCAquOyQQuvvUSH2rnlG51/ruWFgqUCAwEAAaOCAWUwggFhMB8GA1UdIwQY
MBaAFLuvfgI9+qbxPISOre44mOzZMjLUMB0GA1UdDgQWBBSQr2o6lFoL2JDqElZz
30O0Oija5zAOBgNVHQ8BAf8EBAMCAYYwEgYDVR0TAQH/BAgwBgEB/wIBADAdBgNV
HSUEFjAUBggrBgEFBQcDAQYIKwYBBQUHAwIwGwYDVR0gBBQwEjAGBgRVHSAAMAgG
BmeBDAECATBMBgNVHR8ERTBDMEGgP6A9hjtodHRwOi8vY3JsLmNvbW9kb2NhLmNv
bS9DT01PRE9SU0FDZXJ0aWZpY2F0aW9uQXV0aG9yaXR5LmNybDBxBggrBgEFBQcB
AQRlMGMwOwYIKwYBBQUHMAKGL2h0dHA6Ly9jcnQuY29tb2RvY2EuY29tL0NPTU9E
T1JTQUFkZFRydXN0Q0EuY3J0MCQGCCsGAQUFBzABhhhodHRwOi8vb2NzcC5jb21v
ZG9jYS5jb20wDQYJKoZIhvcNAQEMBQADggIBAE4rdk+SHGI2ibp3wScF9BzWRJ2p
mj6q1WZmAT7qSeaiNbz69t2Vjpk1mA42GHWx3d1Qcnyu3HeIzg/3kCDKo2cuH1Z/
e+FE6kKVxF0NAVBGFfKBiVlsit2M8RKhjTpCipj4SzR7JzsItG8kO3KdY3RYPBps
P0/HEZrIqPW1N+8QRcZs2eBelSaz662jue5/DJpmNXMyYE7l3YphLG5SEXdoltMY
dVEVABt0iN3hxzgEQyjpFv3ZBdRdRydg1vs4O2xyopT4Qhrf7W8GjEXCBgCq5Ojc
2bXhc3js9iPc0d1sjhqPpepUfJa3w/5Vjo1JXvxku88+vZbrac2/4EjxYoIQ5QxG
V/Iz2tDIY+3GH5QFlkoakdH368+PUq4NCNk+qKBR6cGHdNXJ93SrLlP7u3r7l+L4
HyaPs9Kg4DdbKDsx5Q5XLVq4rXmsXiBmGqW5prU5wfWYQ//u+aen/e7KJD2AFsQX
j4rBYKEMrltDR5FL1ZoXX/nUh8HCjLfn4g8wGTeGrODcQgPmlKidrv0PJFGUzpII
0fxQ8ANAe4hZ7Q7drNJ3gjTcBpUC2JD5Leo31Rpg0Gcg19hCC0Wvgmje3WYkN5Ap
lBlGGSW4gNfL1IYoakRwJiNiqZ+Gb7+6kHDSVneFeO/qJakXzlByjAA6quPbYzSf
+AZxAeKCINT+b72x
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIIFdDCCBFygAwIBAgIQJ2buVutJ846r13Ci/ITeIjANBgkqhkiG9w0BAQwFADBv
MQswCQYDVQQGEwJTRTEUMBIGA1UEChMLQWRkVHJ1c3QgQUIxJjAkBgNVBAsTHUFk
ZFRydXN0IEV4dGVybmFsIFRUUCBOZXR3b3JrMSIwIAYDVQQDExlBZGRUcnVzdCBF
eHRlcm5hbCBDQSBSb290MB4XDTAwMDUzMDEwNDgzOFoXDTIwMDUzMDEwNDgzOFow
gYUxCzAJBgNVBAYTAkdCMRswGQYDVQQIExJHcmVhdGVyIE1hbmNoZXN0ZXIxEDAO
BgNVBAcTB1NhbGZvcmQxGjAYBgNVBAoTEUNPTU9ETyBDQSBMaW1pdGVkMSswKQYD
VQQDEyJDT01PRE8gUlNBIENlcnRpZmljYXRpb24gQXV0aG9yaXR5MIICIjANBgkq
hkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAkehUktIKVrGsDSTdxc9EZ3SZKzejfSNw
AHG8U9/E+ioSj0t/EFa9n3Byt2F/yUsPF6c947AEYe7/EZfH9IY+Cvo+XPmT5jR6
2RRr55yzhaCCenavcZDX7P0N+pxs+t+wgvQUfvm+xKYvT3+Zf7X8Z0NyvQwA1onr
ayzT7Y+YHBSrfuXjbvzYqOSSJNpDa2K4Vf3qwbxstovzDo2a5JtsaZn4eEgwRdWt
4Q08RWD8MpZRJ7xnw8outmvqRsfHIKCxH2XeSAi6pE6p8oNGN4Tr6MyBSENnTnIq
m1y9TBsoilwie7SrmNnu4FGDwwlGTm0+mfqVF9p8M1dBPI1R7Qu2XK8sYxrfV8g/
vOldxJuvRZnio1oktLqpVj3Pb6r/SVi+8Kj/9Lit6Tf7urj0Czr56ENCHonYhMsT
8dm74YlguIwoVqwUHZwK53Hrzw7dPamWoUi9PPevtQ0iTMARgexWO/bTouJbt7IE
IlKVgJNp6I5MZfGRAy1wdALqi2cVKWlSArvX31BqVUa/oKMoYX9w0MOiqiwhqkfO
KJwGRXa/ghgntNWutMtQ5mv0TIZxMOmm3xaG4Nj/QN370EKIf6MzOi5cHkERgWPO
GHFrK+ymircxXDpqR+DDeVnWIBqv8mqYqnK8V0rSS527EPywTEHl7R09XiidnMy/
s1Hap0flhFMCAwEAAaOB9DCB8TAfBgNVHSMEGDAWgBStvZh6NLQm9/rEJlTvA73g
JMtUGjAdBgNVHQ4EFgQUu69+Aj36pvE8hI6t7jiY7NkyMtQwDgYDVR0PAQH/BAQD
AgGGMA8GA1UdEwEB/wQFMAMBAf8wEQYDVR0gBAowCDAGBgRVHSAAMEQGA1UdHwQ9
MDswOaA3oDWGM2h0dHA6Ly9jcmwudXNlcnRydXN0LmNvbS9BZGRUcnVzdEV4dGVy
bmFsQ0FSb290LmNybDA1BggrBgEFBQcBAQQpMCcwJQYIKwYBBQUHMAGGGWh0dHA6
Ly9vY3NwLnVzZXJ0cnVzdC5jb20wDQYJKoZIhvcNAQEMBQADggEBAGS/g/FfmoXQ
zbihKVcN6Fr30ek+8nYEbvFScLsePP9NDXRqzIGCJdPDoCpdTPW6i6FtxFQJdcfj
Jw5dhHk3QBN39bSsHNA7qxcS1u80GH4r6XnTq1dFDK8o+tDb5VCViLvfhVdpfZLY
Uspzgb8c8+a4bmYRBbMelC1/kZWSWfFMzqORcUx8Rww7Cxn2obFshj5cqsQugsv5
B5a6SE2Q8pTIqXOi6wZ7I53eovNNVZ96YUWYGGjHXkBrI/V5eu+MtWuLt29G9Hvx
PUsE2JOAWVrgQSQdso8VYFhH2+9uRv0V9dlfmrPb2LjkQLPNlzmuhbsdjrzch5vR
pu/xO28QOG8=
-----END CERTIFICATE-----
";

        private const string TestKeyPart =
    @"-----BEGIN PRIVATE KEY-----
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
0000000000000000000000000000000000000000000000000000000000000000
00000000000000000000000=
-----END PRIVATE KEY-----
";
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void NormalizePem()
        {
            Assert.Equal("abcde\n", TlsCertificate.NormalizePem("abcde"));
            Assert.Equal("abcde\n", TlsCertificate.NormalizePem("abcde\n"));
            Assert.Equal("abcde\n", TlsCertificate.NormalizePem("abcde\r\n"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void Constructor()
        {
            var cert = new TlsCertificate(TestCertPart + TestKeyPart);

            Assert.Equal(TlsCertificate.NormalizePem(TestCertPart), cert.CertPem);
            Assert.Equal(TlsCertificate.NormalizePem(TestKeyPart), cert.KeyPem);

            cert = new TlsCertificate(TestCertPart, TestKeyPart);

            Assert.Equal(TlsCertificate.NormalizePem(TestCertPart), cert.CertPem);
            Assert.Equal(TlsCertificate.NormalizePem(TestKeyPart), cert.KeyPem);

            Assert.Equal(TlsCertificate.NormalizePem(TestCertPart) + TlsCertificate.NormalizePem(TestKeyPart), cert.CombinedPem);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void ConstructorErrors()
        {
            Assert.Throws<ArgumentException>(() => new TlsCertificate("not a cert"));
            Assert.Throws<ArgumentException>(() => new TlsCertificate("-----BEGIN PRIVATE KEY-----\n" + "-----BEGIN CERTIFICATE-----\n"));
            Assert.Throws<ArgumentException>(() => new TlsCertificate("not a cert", "-----BEGIN PRIVATE KEY-----\n"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void Load()
        {
            using (var tempFolder = new TempFolder())
            {
                var path1 = Path.Combine(tempFolder.Path, "test1");
                var path2 = Path.Combine(tempFolder.Path, "test2");

                // Separate files with CRLF line endings.

                File.WriteAllText(path1, TestCertPart);
                File.WriteAllText(path2, TestKeyPart);

                var cert = TlsCertificate.Load(path1, path2);

                Assert.Equal(TlsCertificate.NormalizePem(TestCertPart), cert.CertPem);
                Assert.Equal(TlsCertificate.NormalizePem(TestKeyPart), cert.KeyPem);

                // Separate files with LF line endings.

                File.WriteAllText(path1, TlsCertificate.NormalizePem(TestCertPart));
                File.WriteAllText(path2, TlsCertificate.NormalizePem(TestKeyPart));

                cert = TlsCertificate.Load(path1, path2);

                Assert.Equal(TlsCertificate.NormalizePem(TestCertPart), cert.CertPem);
                Assert.Equal(TlsCertificate.NormalizePem(TestKeyPart), cert.KeyPem);

                // Combined file with CRLF line endings.

                File.WriteAllText(path1, TestCertPart + TestKeyPart);

                cert = TlsCertificate.Load(path1);

                File.WriteAllText(path1, TlsCertificate.NormalizePem(TestCertPart));
                File.WriteAllText(path2, TlsCertificate.NormalizePem(TestKeyPart));

                // Combined file with LF line endings.

                File.WriteAllText(path1, TlsCertificate.NormalizePem(TestCertPart) + TlsCertificate.NormalizePem(TestKeyPart));

                cert = TlsCertificate.Load(path1);

                File.WriteAllText(path1, TlsCertificate.NormalizePem(TestCertPart));
                File.WriteAllText(path2, TlsCertificate.NormalizePem(TestKeyPart));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void ParseCertUtil_SAN()
        {
            // Verify a [CertUtil] dump of a SAN certificate.

            const string dump =
@"X509 Certificate:
Version: 3
Serial Number: f7c33b6eed61a3695c1a61e77e7d349f
Signature Algorithm:
    Algorithm ObjectId: 1.2.840.113549.1.1.11 sha256RSA
    Algorithm Parameters:
    05 00
Issuer:
    CN=COMODO RSA Domain Validation Secure Server CA
    O=COMODO CA Limited
    L=Salford
    S=Greater Manchester
    C=GB
  Name Hash(sha1): 7ae13ee8a0c42a2cb428cbe7a605461940e2a1e9
  Name Hash(md5): 737301010f9ec759d54329bbb1553aa2

 NotBefore: 10/15/2016 4:00 PM
 NotAfter: 10/16/2017 3:59 PM

Subject:
    CN=*.neontest.com
    OU=PositiveSSL Wildcard
    OU=Domain Control Validated
  Name Hash(sha1): 21a9a243dec2654cc845de819db21f9828960a44
  Name Hash(md5): b663f495938586143c2e4ab879f89fae

Public Key Algorithm:
    Algorithm ObjectId: 1.2.840.113549.1.1.1 RSA
    Algorithm Parameters:
    05 00
Public Key Length: 2048 bits
Public Key: UnusedBits = 0
    0000  30 82 01 0a 02 82 01 01  00 e3 40 b1 8a 4f ce 50
    0010  71 5d 00 8f e7 b2 f0 52  22 2d 7b f4 97 01 e6 d5
    0020  cf 37 2f 62 a8 1b af 87  ca 26 d6 9a 83 f9 21 25
    0030  2d 4e f8 f7 85 7b 65 06  1b 17 de 53 e7 4f 77 b1
    0040  ac 71 d5 49 7e 9b f8 42  48 3a 83 af 3b 03 87 c8
    0050  c6 d1 2e f8 cb fa 5b d5  9f f3 68 b6 c4 87 82 9f
    0060  9c e3 b7 c3 7b 71 cb bc  f9 00 1b 0d 7e b2 ae 7a
    0070  50 8f cb 0c 01 e5 6b 72  a3 dc 08 a1 f3 53 88 84
    0080  92 5c 3b 88 28 20 de 39  22 ac 6e 53 99 cf 43 dd
    0090  20 ee 2e 1c 02 f4 42 13  84 75 03 17 0c bf 46 59
    00a0  44 70 ac fa 3e 2d d9 ca  47 6e a8 a2 13 72 5e d5
    00b0  fd 4b 60 99 27 01 35 a3  1a 70 9a 9d 48 bb 89 14
    00c0  0b ed a7 de 90 90 25 db  31 81 33 96 c5 7f 7a b6
    00d0  61 db 22 8e 93 5d a0 e9  02 a9 f3 05 72 3f 79 ed
    00e0  fa 69 c3 a9 e5 ef 5c 7f  db 36 aa df b6 76 16 fc
    00f0  b6 f2 0b b8 cb 21 8e e6  00 85 35 d8 7e 01 c1 fb
    0100  78 b5 ba 4e 91 4e dd 9f  4f 02 03 01 00 01
Certificate Extensions: 9
    2.5.29.35: Flags = 0, Length = 18
    Authority Key Identifier
        KeyID=90 af 6a 3a 94 5a 0b d8 90 ea 12 56 73 df 43 b4 3a 28 da e7

    2.5.29.14: Flags = 0, Length = 16
    Subject Key Identifier
        70 ac 36 1f 8e 34 33 4a 41 95 7b d5 ef 3d d8 98 6c d4 c8 d9

    2.5.29.15: Flags = 1(Critical), Length = 4
    Key Usage
        Digital Signature, Key Encipherment (a0)

    2.5.29.19: Flags = 1(Critical), Length = 2
    Basic Constraints
        Subject Type=End Entity
        Path Length Constraint=None

    2.5.29.37: Flags = 0, Length = 16
    Enhanced Key Usage
        Server Authentication (1.3.6.1.5.5.7.3.1)
        Client Authentication (1.3.6.1.5.5.7.3.2)

    2.5.29.32: Flags = 0, Length = 48
    Certificate Policies
        [1]Certificate Policy:
             Policy Identifier=1.3.6.1.4.1.6449.1.2.2.7
             [1,1]Policy Qualifier Info:
                  Policy Qualifier Id=CPS
                  Qualifier:
                       https://secure.comodo.com/CPS
        [2]Certificate Policy:
             Policy Identifier=2.23.140.1.2.1

    2.5.29.31: Flags = 0, Length = 4d
    CRL Distribution Points
        [1]CRL Distribution Point
             Distribution Point Name:
                  Full Name:
                       URL=http://crl.comodoca.com/COMODORSADomainValidationSecureServerCA.crl

    1.3.6.1.5.5.7.1.1: Flags = 0, Length = 79
    Authority Information Access
        [1]Authority Info Access
             Access Method=Certification Authority Issuer (1.3.6.1.5.5.7.48.2)
             Alternative Name:
                  URL=http://crt.comodoca.com/COMODORSADomainValidationSecureServerCA.crt
        [2]Authority Info Access
             Access Method=On-line Certificate Status Protocol (1.3.6.1.5.5.7.48.1)
             Alternative Name:
                  URL=http://ocsp.comodoca.com

    2.5.29.17: Flags = 0, Length = 20
    Subject Alternative Name
        DNS Name=*.neontest.com
        DNS Name=neontest.com

Signature Algorithm:
    Algorithm ObjectId: 1.2.840.113549.1.1.11 sha256RSA
    Algorithm Parameters:
    05 00
Signature: UnusedBits=0
    0000  1b 85 72 a2 5e 38 c7 3c  be 48 59 c5 b5 9c 87 03
    0010  c6 4e 9c 52 b0 20 6a 14  4a 85 75 3f 59 af 57 92
    0020  14 1c 0c 69 c6 7a 42 3d  cd c8 a7 95 a9 b8 47 7e
    0030  ed f2 63 25 10 8b bf 0c  d8 0f 46 29 b5 78 9f 13
    0040  d9 0f 34 84 c9 83 c3 1a  97 07 57 dd 66 22 c5 19
    0050  77 4c ea 04 97 79 cd 3b  f2 02 44 f1 89 ee 6b 0a
    0060  e3 d5 df 86 c6 8a 3b 98  7d 21 20 2d 10 09 54 c3
    0070  9c a9 6b 94 70 5b 8a ed  97 54 b4 d9 74 22 f5 1e
    0080  78 d7 7e a8 cf ef 21 57  ee 3c d5 45 45 25 74 ac
    0090  56 c6 0f 56 b4 42 51 0a  86 e9 02 bb 93 1d 06 0c
    00a0  4b ad 4b 27 63 29 11 f0  d2 2f 97 4b b8 04 54 d2
    00b0  d6 dd 20 ee f2 a8 bf d0  20 f0 0f e0 45 92 60 ad
    00c0  50 82 cd 4a a2 63 bb f7  a5 83 68 ec 4a 1d 05 ae
    00d0  78 57 e0 15 f8 b0 bd 4f  67 14 25 9c d8 96 bf 2b
    00e0  7c b4 fc b6 3b 90 ca 77  3e 67 e4 9d 88 a6 08 d5
    00f0  52 bc 1e a0 91 6f 6b c4  45 2d e2 4b 66 35 a5 49
Non-root Certificate
Key Id Hash(rfc-sha1): 70 ac 36 1f 8e 34 33 4a 41 95 7b d5 ef 3d d8 98 6c d4 c8 d9
Key Id Hash(sha1): 6a cd 98 59 03 c9 4d 39 5d fa 68 2d e9 ed 2d f5 78 b7 49 2a
Key Id Hash(md5): 95be85460316d2476c909c824ec6108b
Key Id Hash(sha256): f5ad0d32302d410daad3f39ddbb2e1a52e79fcd8bff1c4d77e028830844bb363
Cert Hash(md5): 99 d4 81 bc 75 c7 fb 36 e3 ba ec e4 b5 a6 21 6d
Cert Hash(sha1): 83 db 76 4a 8f a2 cd c9 a0 12 d5 ff 6f 0d 46 1c 82 3c ac ac
Cert Hash(sha256): 87b1a786fe76f7498831ea654a567ebe293763a396d43ecf039ea7041ed6ee63
Signature Hash: 90c9102b85154435565bfa90928858d5b9eafd9a714d320efa0da0c9c5062ad1
CertUtil: -dump command completed successfully.
";

            var cert = new TlsCertificate();

            cert.ParseCertUtil(dump);

            Assert.Equal(new DateTime(2016, 10, 15, 16, 00, 00, DateTimeKind.Utc), cert.ValidFrom);
            Assert.Equal(new DateTime(2017, 10, 16, 15, 59, 00, DateTimeKind.Utc), cert.ValidUntil);
            Assert.Equal(new string[] { "*.neontest.com", "neontest.com" }, cert.Hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void ParseCertUtil()
        {
            // Verify a [CertUtil] dump of a non-SAN certificate.

            // $todo(jefflill): 
            //
            // I just manually edited a SAN certificate to remove the [Subject Alternative Name]
            // part.  I should redo this at somepoint to use a legitimate non-SAN cert dump.

            const string dump =
@"X509 Certificate:
Version: 3
Serial Number: f7c33b6eed61a3695c1a61e77e7d349f
Signature Algorithm:
    Algorithm ObjectId: 1.2.840.113549.1.1.11 sha256RSA
    Algorithm Parameters:
    05 00
Issuer:
    CN=COMODO RSA Domain Validation Secure Server CA
    O=COMODO CA Limited
    L=Salford
    S=Greater Manchester
    C=GB
  Name Hash(sha1): 7ae13ee8a0c42a2cb428cbe7a605461940e2a1e9
  Name Hash(md5): 737301010f9ec759d54329bbb1553aa2

 NotBefore: 10/15/2016 4:00 PM
 NotAfter: 10/16/2017 3:59 PM

Subject:
    CN=*.neontest.com
    OU=PositiveSSL Wildcard
    OU=Domain Control Validated
  Name Hash(sha1): 21a9a243dec2654cc845de819db21f9828960a44
  Name Hash(md5): b663f495938586143c2e4ab879f89fae

Public Key Algorithm:
    Algorithm ObjectId: 1.2.840.113549.1.1.1 RSA
    Algorithm Parameters:
    05 00
Public Key Length: 2048 bits
Public Key: UnusedBits = 0
    0000  30 82 01 0a 02 82 01 01  00 e3 40 b1 8a 4f ce 50
    0010  71 5d 00 8f e7 b2 f0 52  22 2d 7b f4 97 01 e6 d5
    0020  cf 37 2f 62 a8 1b af 87  ca 26 d6 9a 83 f9 21 25
    0030  2d 4e f8 f7 85 7b 65 06  1b 17 de 53 e7 4f 77 b1
    0040  ac 71 d5 49 7e 9b f8 42  48 3a 83 af 3b 03 87 c8
    0050  c6 d1 2e f8 cb fa 5b d5  9f f3 68 b6 c4 87 82 9f
    0060  9c e3 b7 c3 7b 71 cb bc  f9 00 1b 0d 7e b2 ae 7a
    0070  50 8f cb 0c 01 e5 6b 72  a3 dc 08 a1 f3 53 88 84
    0080  92 5c 3b 88 28 20 de 39  22 ac 6e 53 99 cf 43 dd
    0090  20 ee 2e 1c 02 f4 42 13  84 75 03 17 0c bf 46 59
    00a0  44 70 ac fa 3e 2d d9 ca  47 6e a8 a2 13 72 5e d5
    00b0  fd 4b 60 99 27 01 35 a3  1a 70 9a 9d 48 bb 89 14
    00c0  0b ed a7 de 90 90 25 db  31 81 33 96 c5 7f 7a b6
    00d0  61 db 22 8e 93 5d a0 e9  02 a9 f3 05 72 3f 79 ed
    00e0  fa 69 c3 a9 e5 ef 5c 7f  db 36 aa df b6 76 16 fc
    00f0  b6 f2 0b b8 cb 21 8e e6  00 85 35 d8 7e 01 c1 fb
    0100  78 b5 ba 4e 91 4e dd 9f  4f 02 03 01 00 01
Certificate Extensions: 9
    2.5.29.35: Flags = 0, Length = 18
    Authority Key Identifier
        KeyID=90 af 6a 3a 94 5a 0b d8 90 ea 12 56 73 df 43 b4 3a 28 da e7

    2.5.29.14: Flags = 0, Length = 16
    Subject Key Identifier
        70 ac 36 1f 8e 34 33 4a 41 95 7b d5 ef 3d d8 98 6c d4 c8 d9

    2.5.29.15: Flags = 1(Critical), Length = 4
    Key Usage
        Digital Signature, Key Encipherment (a0)

    2.5.29.19: Flags = 1(Critical), Length = 2
    Basic Constraints
        Subject Type=End Entity
        Path Length Constraint=None

    2.5.29.37: Flags = 0, Length = 16
    Enhanced Key Usage
        Server Authentication (1.3.6.1.5.5.7.3.1)
        Client Authentication (1.3.6.1.5.5.7.3.2)

    2.5.29.32: Flags = 0, Length = 48
    Certificate Policies
        [1]Certificate Policy:
             Policy Identifier=1.3.6.1.4.1.6449.1.2.2.7
             [1,1]Policy Qualifier Info:
                  Policy Qualifier Id=CPS
                  Qualifier:
                       https://secure.comodo.com/CPS
        [2]Certificate Policy:
             Policy Identifier=2.23.140.1.2.1

    2.5.29.31: Flags = 0, Length = 4d
    CRL Distribution Points
        [1]CRL Distribution Point
             Distribution Point Name:
                  Full Name:
                       URL=http://crl.comodoca.com/COMODORSADomainValidationSecureServerCA.crl

    1.3.6.1.5.5.7.1.1: Flags = 0, Length = 79
    Authority Information Access
        [1]Authority Info Access
             Access Method=Certification Authority Issuer (1.3.6.1.5.5.7.48.2)
             Alternative Name:
                  URL=http://crt.comodoca.com/COMODORSADomainValidationSecureServerCA.crt
        [2]Authority Info Access
             Access Method=On-line Certificate Status Protocol (1.3.6.1.5.5.7.48.1)
             Alternative Name:
                  URL=http://ocsp.comodoca.com

Signature Algorithm:
    Algorithm ObjectId: 1.2.840.113549.1.1.11 sha256RSA
    Algorithm Parameters:
    05 00
Signature: UnusedBits=0
    0000  1b 85 72 a2 5e 38 c7 3c  be 48 59 c5 b5 9c 87 03
    0010  c6 4e 9c 52 b0 20 6a 14  4a 85 75 3f 59 af 57 92
    0020  14 1c 0c 69 c6 7a 42 3d  cd c8 a7 95 a9 b8 47 7e
    0030  ed f2 63 25 10 8b bf 0c  d8 0f 46 29 b5 78 9f 13
    0040  d9 0f 34 84 c9 83 c3 1a  97 07 57 dd 66 22 c5 19
    0050  77 4c ea 04 97 79 cd 3b  f2 02 44 f1 89 ee 6b 0a
    0060  e3 d5 df 86 c6 8a 3b 98  7d 21 20 2d 10 09 54 c3
    0070  9c a9 6b 94 70 5b 8a ed  97 54 b4 d9 74 22 f5 1e
    0080  78 d7 7e a8 cf ef 21 57  ee 3c d5 45 45 25 74 ac
    0090  56 c6 0f 56 b4 42 51 0a  86 e9 02 bb 93 1d 06 0c
    00a0  4b ad 4b 27 63 29 11 f0  d2 2f 97 4b b8 04 54 d2
    00b0  d6 dd 20 ee f2 a8 bf d0  20 f0 0f e0 45 92 60 ad
    00c0  50 82 cd 4a a2 63 bb f7  a5 83 68 ec 4a 1d 05 ae
    00d0  78 57 e0 15 f8 b0 bd 4f  67 14 25 9c d8 96 bf 2b
    00e0  7c b4 fc b6 3b 90 ca 77  3e 67 e4 9d 88 a6 08 d5
    00f0  52 bc 1e a0 91 6f 6b c4  45 2d e2 4b 66 35 a5 49
Non-root Certificate
Key Id Hash(rfc-sha1): 70 ac 36 1f 8e 34 33 4a 41 95 7b d5 ef 3d d8 98 6c d4 c8 d9
Key Id Hash(sha1): 6a cd 98 59 03 c9 4d 39 5d fa 68 2d e9 ed 2d f5 78 b7 49 2a
Key Id Hash(md5): 95be85460316d2476c909c824ec6108b
Key Id Hash(sha256): f5ad0d32302d410daad3f39ddbb2e1a52e79fcd8bff1c4d77e028830844bb363
Cert Hash(md5): 99 d4 81 bc 75 c7 fb 36 e3 ba ec e4 b5 a6 21 6d
Cert Hash(sha1): 83 db 76 4a 8f a2 cd c9 a0 12 d5 ff 6f 0d 46 1c 82 3c ac ac
Cert Hash(sha256): 87b1a786fe76f7498831ea654a567ebe293763a396d43ecf039ea7041ed6ee63
Signature Hash: 90c9102b85154435565bfa90928858d5b9eafd9a714d320efa0da0c9c5062ad1
CertUtil: -dump command completed successfully.
";

            var cert = new TlsCertificate();

            cert.ParseCertUtil(dump);

            Assert.Equal(new DateTime(2016, 10, 15, 16, 00, 00, DateTimeKind.Utc), cert.ValidFrom);
            Assert.Equal(new DateTime(2017, 10, 16, 15, 59, 00, DateTimeKind.Utc), cert.ValidUntil);
            Assert.Equal(new string[] { "*.neontest.com" }, cert.Hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void ParseOpenSSL_SAN()
        {
            // Verify an [OpenSSL] dump of a SAN certificate.

            const string dump =
@"Certificate:
    Data:
        Version: 3 (0x2)
        Serial Number:
            f7:c3:3b:6e:ed:61:a3:69:5c:1a:61:e7:7e:7d:34:9f
    Signature Algorithm: sha256WithRSAEncryption
        Issuer: C=GB, ST=Greater Manchester, L=Salford, O=COMODO CA Limited, CN=COMODO RSA Domain Validation Secure Server CA
        Validity
            Not Before: Oct 16 00:00:00 2016 GMT
            Not After : Oct 16 23:59:59 2017 GMT
        Subject: OU=Domain Control Validated, OU=PositiveSSL Wildcard, CN=*.neontest.com
        Subject Public Key Info:
            Public Key Algorithm: rsaEncryption
                Public-Key: (2048 bit)
                Modulus:
                    00:e3:40:b1:8a:4f:ce:50:71:5d:00:8f:e7:b2:f0:
                    52:22:2d:7b:f4:97:01:e6:d5:cf:37:2f:62:a8:1b:
                    af:87:ca:26:d6:9a:83:f9:21:25:2d:4e:f8:f7:85:
                    7b:65:06:1b:17:de:53:e7:4f:77:b1:ac:71:d5:49:
                    7e:9b:f8:42:48:3a:83:af:3b:03:87:c8:c6:d1:2e:
                    f8:cb:fa:5b:d5:9f:f3:68:b6:c4:87:82:9f:9c:e3:
                    b7:c3:7b:71:cb:bc:f9:00:1b:0d:7e:b2:ae:7a:50:
                    8f:cb:0c:01:e5:6b:72:a3:dc:08:a1:f3:53:88:84:
                    92:5c:3b:88:28:20:de:39:22:ac:6e:53:99:cf:43:
                    dd:20:ee:2e:1c:02:f4:42:13:84:75:03:17:0c:bf:
                    46:59:44:70:ac:fa:3e:2d:d9:ca:47:6e:a8:a2:13:
                    72:5e:d5:fd:4b:60:99:27:01:35:a3:1a:70:9a:9d:
                    48:bb:89:14:0b:ed:a7:de:90:90:25:db:31:81:33:
                    96:c5:7f:7a:b6:61:db:22:8e:93:5d:a0:e9:02:a9:
                    f3:05:72:3f:79:ed:fa:69:c3:a9:e5:ef:5c:7f:db:
                    36:aa:df:b6:76:16:fc:b6:f2:0b:b8:cb:21:8e:e6:
                    00:85:35:d8:7e:01:c1:fb:78:b5:ba:4e:91:4e:dd:
                    9f:4f
                Exponent: 65537 (0x10001)
        X509v3 extensions:
            X509v3 Authority Key Identifier: 
                keyid:90:AF:6A:3A:94:5A:0B:D8:90:EA:12:56:73:DF:43:B4:3A:28:DA:E7

            X509v3 Subject Key Identifier: 
                70:AC:36:1F:8E:34:33:4A:41:95:7B:D5:EF:3D:D8:98:6C:D4:C8:D9
            X509v3 Key Usage: critical
                Digital Signature, Key Encipherment
            X509v3 Basic Constraints: critical
                CA:FALSE
            X509v3 Extended Key Usage: 
                TLS Web Server Authentication, TLS Web Client Authentication
            X509v3 Certificate Policies: 
                Policy: 1.3.6.1.4.1.6449.1.2.2.7
                  CPS: https://secure.comodo.com/CPS
                Policy: 2.23.140.1.2.1

            X509v3 CRL Distribution Points: 

                Full Name:
                  URI:http://crl.comodoca.com/COMODORSADomainValidationSecureServerCA.crl

            Authority Information Access: 
                CA Issuers - URI:http://crt.comodoca.com/COMODORSADomainValidationSecureServerCA.crt
                OCSP - URI:http://ocsp.comodoca.com

            X509v3 Subject Alternative Name: 
                DNS:*.neontest.com, DNS:neontest.com
    Signature Algorithm: sha256WithRSAEncryption
         49:a5:35:66:4b:e2:2d:45:c4:6b:6f:91:a0:1e:bc:52:d5:08:
         a6:88:9d:e4:67:3e:77:ca:90:3b:b6:fc:b4:7c:2b:bf:96:d8:
         9c:25:14:67:4f:bd:b0:f8:15:e0:57:78:ae:05:1d:4a:ec:68:
         83:a5:f7:bb:63:a2:4a:cd:82:50:ad:60:92:45:e0:0f:f0:20:
         d0:bf:a8:f2:ee:20:dd:d6:d2:54:04:b8:4b:97:2f:d2:f0:11:
         29:63:27:4b:ad:4b:0c:06:1d:93:bb:02:e9:86:0a:51:42:b4:
         56:0f:c6:56:ac:74:25:45:45:d5:3c:ee:57:21:ef:cf:a8:7e:
         d7:78:1e:f5:22:74:d9:b4:54:97:ed:8a:5b:70:94:6b:a9:9c:
         c3:54:09:10:2d:20:21:7d:98:3b:8a:c6:86:df:d5:e3:0a:6b:
         ee:89:f1:44:02:f2:3b:cd:79:97:04:ea:4c:77:19:c5:22:66:
         dd:57:07:97:1a:c3:83:c9:84:34:0f:d9:13:9f:78:b5:29:46:
         0f:d8:0c:bf:8b:10:25:63:f2:ed:7e:47:b8:a9:95:a7:c8:cd:
         3d:42:7a:c6:69:0c:1c:14:92:57:af:59:3f:75:85:4a:14:6a:
         20:b0:52:9c:4e:c6:03:87:9c:b5:c5:59:48:be:3c:c7:38:5e:
         a2:72:85:1b
-----BEGIN CERTIFICATE-----
MIIFUjCCBDqgAwIBAgIRAPfDO27tYaNpXBph5359NJ8wDQYJKoZIhvcNAQELBQAw
gZAxCzAJBgNVBAYTAkdCMRswGQYDVQQIExJHcmVhdGVyIE1hbmNoZXN0ZXIxEDAO
BgNVBAcTB1NhbGZvcmQxGjAYBgNVBAoTEUNPTU9ETyBDQSBMaW1pdGVkMTYwNAYD
VQQDEy1DT01PRE8gUlNBIERvbWFpbiBWYWxpZGF0aW9uIFNlY3VyZSBTZXJ2ZXIg
Q0EwHhcNMTYxMDE2MDAwMDAwWhcNMTcxMDE2MjM1OTU5WjBbMSEwHwYDVQQLExhE
b21haW4gQ29udHJvbCBWYWxpZGF0ZWQxHTAbBgNVBAsTFFBvc2l0aXZlU1NMIFdp
bGRjYXJkMRcwFQYDVQQDDA4qLm5lb250ZXN0LmNvbTCCASIwDQYJKoZIhvcNAQEB
BQADggEPADCCAQoCggEBAONAsYpPzlBxXQCP57LwUiIte/SXAebVzzcvYqgbr4fK
Jtaag/khJS1O+PeFe2UGGxfeU+dPd7GscdVJfpv4Qkg6g687A4fIxtEu+Mv6W9Wf
82i2xIeCn5zjt8N7ccu8+QAbDX6yrnpQj8sMAeVrcqPcCKHzU4iEklw7iCgg3jki
rG5Tmc9D3SDuLhwC9EIThHUDFwy/RllEcKz6Pi3ZykduqKITcl7V/UtgmScBNaMa
cJqdSLuJFAvtp96QkCXbMYEzlsV/erZh2yKOk12g6QKp8wVyP3nt+mnDqeXvXH/b
NqrftnYW/LbyC7jLIY7mAIU12H4Bwft4tbpOkU7dn08CAwEAAaOCAdkwggHVMB8G
A1UdIwQYMBaAFJCvajqUWgvYkOoSVnPfQ7Q6KNrnMB0GA1UdDgQWBBRwrDYfjjQz
SkGVe9XvPdiYbNTI2TAOBgNVHQ8BAf8EBAMCBaAwDAYDVR0TAQH/BAIwADAdBgNV
HSUEFjAUBggrBgEFBQcDAQYIKwYBBQUHAwIwTwYDVR0gBEgwRjA6BgsrBgEEAbIx
AQICBzArMCkGCCsGAQUFBwIBFh1odHRwczovL3NlY3VyZS5jb21vZG8uY29tL0NQ
UzAIBgZngQwBAgEwVAYDVR0fBE0wSzBJoEegRYZDaHR0cDovL2NybC5jb21vZG9j
YS5jb20vQ09NT0RPUlNBRG9tYWluVmFsaWRhdGlvblNlY3VyZVNlcnZlckNBLmNy
bDCBhQYIKwYBBQUHAQEEeTB3ME8GCCsGAQUFBzAChkNodHRwOi8vY3J0LmNvbW9k
b2NhLmNvbS9DT01PRE9SU0FEb21haW5WYWxpZGF0aW9uU2VjdXJlU2VydmVyQ0Eu
Y3J0MCQGCCsGAQUFBzABhhhodHRwOi8vb2NzcC5jb21vZG9jYS5jb20wJwYDVR0R
BCAwHoIOKi5uZW9udGVzdC5jb22CDG5lb250ZXN0LmNvbTANBgkqhkiG9w0BAQsF
AAOCAQEASaU1ZkviLUXEa2+RoB68UtUIpoid5Gc+d8qQO7b8tHwrv5bYnCUUZ0+9
sPgV4Fd4rgUdSuxog6X3u2OiSs2CUK1gkkXgD/Ag0L+o8u4g3dbSVAS4S5cv0vAR
KWMnS61LDAYdk7sC6YYKUUK0Vg/GVqx0JUVF1TzuVyHvz6h+13ge9SJ02bRUl+2K
W3CUa6mcw1QJEC0gIX2YO4rGht/V4wpr7onxRALyO815lwTqTHcZxSJm3VcHlxrD
g8mENA/ZE594tSlGD9gMv4sQJWPy7X5HuKmVp8jNPUJ6xmkMHBSSV69ZP3WFShRq
ILBSnE7GA4ectcVZSL48xzheonKFGw==
-----END CERTIFICATE-----
";

            var cert = new TlsCertificate();

            cert.ParseOpenSsl(dump);

            Assert.Equal(new DateTime(2016, 10, 16, 00, 00, 00, DateTimeKind.Utc), cert.ValidFrom);
            Assert.Equal(new DateTime(2017, 10, 16, 23, 59, 59, DateTimeKind.Utc), cert.ValidUntil);
            Assert.Equal(new string[] { "*.neontest.com", "neontest.com" }, cert.Hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void Verify()
        {
            // Verify basic fields.

            var cert = new TlsCertificate();

            cert.Hosts.Add("foo.com");
            cert.ValidFrom  = DateTime.UtcNow - TimeSpan.FromMinutes(5);
            cert.ValidUntil = DateTime.UtcNow + TimeSpan.FromMinutes(5);

            Assert.True(cert.IsValidDate());
            Assert.True(cert.IsValidDate(DateTime.UtcNow));
            Assert.True(cert.IsValidDate(cert.ValidFrom));
            Assert.True(cert.IsValidDate(cert.ValidUntil));

            Assert.True(cert.IsValidHost("foo.com"));
            Assert.False(cert.IsValidHost("bar.com"));

            // Verify wildcard certs.

            cert.Hosts.Add("*.foo.com");

            Assert.True(cert.IsValidHost("foo.com"));
            Assert.False(cert.IsValidHost("bar.com"));
            Assert.True(cert.IsValidHost("test.foo.com"));
            Assert.True(cert.IsValidHost("bar.foo.com"));
            Assert.False(cert.IsValidHost("foobar.test.foo.com"));

            // Verify SAN certs with different hosts.

            cert.Hosts.Clear();
            cert.Hosts.Add("foo.com");
            cert.Hosts.Add("bar.com");
            cert.Hosts.Add("foobar.com");

            Assert.True(cert.IsValidHost("foo.com"));
            Assert.True(cert.IsValidHost("bar.com"));
            Assert.True(cert.IsValidHost("foobar.com"));
            Assert.False(cert.IsValidHost("test.foo.com"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void SelfSigned_Host()
        {
            var cert = TlsCertificate.CreateSelfSigned("foo.com");

            cert.Parse();
            Assert.NotNull(cert.Thumbprint);
            Assert.NotEmpty(cert.Thumbprint);
            Assert.Single(cert.Hosts);
            Assert.Contains("foo.com", cert.Hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void SelfSigned_SubdomainsOnly()
        {
            var cert = TlsCertificate.CreateSelfSigned("foo.com", wildcard: Wildcard.SubdomainsOnly);

            cert.Parse();
            Assert.NotNull(cert.Thumbprint);
            Assert.NotEmpty(cert.Thumbprint);
            Assert.Single(cert.Hosts);
            Assert.Contains("*.foo.com", cert.Hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void SelfSigned_RootAndSubdomains()
        {
            var cert = TlsCertificate.CreateSelfSigned("foo.com", wildcard: Wildcard.RootAndSubdomains);

            cert.Parse();
            Assert.NotNull(cert.Thumbprint);
            Assert.NotEmpty(cert.Thumbprint);
            Assert.Equal(2, cert.Hosts.Count);
            Assert.Contains("foo.com", cert.Hosts);
            Assert.Contains("*.foo.com", cert.Hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void SelfSigned_OtherFields()
        {
            var cert = TlsCertificate.CreateSelfSigned("foo.com", wildcard: Wildcard.RootAndSubdomains);

            cert.Parse();
            Assert.NotNull(cert.Thumbprint);
            Assert.NotEmpty(cert.Thumbprint);
            Assert.Equal(2, cert.Hosts.Count);
            Assert.Contains("foo.com", cert.Hosts);
            Assert.Contains("*.foo.com", cert.Hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void SelfSigned_MultiHosts()
        {
            var cert = TlsCertificate.CreateSelfSigned(
                new List<string>()
                {
                    "foo.com",
                    "bar.com",
                    "foobar.com",
                    "*.root.com"
                });

            cert.Parse();
            Assert.NotNull(cert.Thumbprint);
            Assert.NotEmpty(cert.Thumbprint);
            Assert.Equal(4, cert.Hosts.Count);
            Assert.Contains("foo.com", cert.Hosts);
            Assert.Contains("bar.com", cert.Hosts);
            Assert.Contains("foobar.com", cert.Hosts);
            Assert.Contains("*.root.com", cert.Hosts);
        }

        [Fact(Skip = "ToX509Certificate() Doesn't work for some reason.")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void X509_SelfSigned()
        {
            // $todo(jefflill):
            //
            // TlsCertificate.ToX509Certificate() is having trouble parsing the
            // private keys generated for self-signed certificates.  It's seeing
            // only three fields when nine are required.  I dumped a private generated
            // key by TlsCertificate.CreateSelfSigned() with OpenSSL and compared 
            // it to a (presumably self-signed client key) generated by Kubernetes 
            // and they both look like they have the same number of parameters; so
            // this is a bit weird.
            //
            // We don't need this functionality right now, so I'm putting this
            // on the backlog.  I hope to be able to address this when we
            // upgrade TlsCertificate to use the improved .NET Core 3.0 
            // crypto APIs.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/438

            var tlsCert = TlsCertificate.CreateSelfSigned("foo.com", wildcard: Wildcard.RootAndSubdomains);

            tlsCert.Parse();
            Assert.NotNull(tlsCert.Thumbprint);
            Assert.NotEmpty(tlsCert.Thumbprint);
            Assert.Equal(2, tlsCert.Hosts.Count);
            Assert.Contains("foo.com", tlsCert.Hosts);
            Assert.Contains("*.foo.com", tlsCert.Hosts);

            var x509Cert = tlsCert.ToX509();

            Assert.NotNull(x509Cert);
            Assert.Equal(tlsCert.Thumbprint, x509Cert.Thumbprint, ignoreCase: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void X509_NoPrivateKey()
        {
            // Verify that we can load a certificate generated by 
            // Kubernetes without a private key.

            var certPem =
@"-----BEGIN CERTIFICATE-----
MIIC8jCCAdqgAwIBAgIIdaABVSybOjowDQYJKoZIhvcNAQELBQAwFTETMBEGA1UE
AxMKa3ViZXJuZXRlczAeFw0xOTAyMTQyMzQ2MDVaFw0yMDAyMTQyMzQ2MDdaMDQx
FzAVBgNVBAoTDnN5c3RlbTptYXN0ZXJzMRkwFwYDVQQDExBrdWJlcm5ldGVzLWFk
bWluMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAzjgtmL1scH9zwMkF
syXmhuhchLDgkZ+CuEIwmACE6AwlHZ6XfEXaRnLUuwmv87S2epj9rwJ1Sp/EchYV
Q6YjOODYfkJXSxCTiiNwBCzGrTDqKa8JBj1Zy9jkC+ZoKoErJFRKlw5ePtxsdUZE
QozQpIFvjACrTcmJtJiaGnY2C/f6Csrf4OXT2ulSYq+AS3bY4cfrcXGdsUTG5ATS
DblHvntjqLi8loEEj2kjDbK82chAkcgPadQ28/P6cqwKnxHAgWeleCSxxNvHE8+O
vJiqrxh7TBUQiFWWSbgAVED8JiN++E779P/MgQCeGYPD3OgrW97T1U7ciQWZ38XB
KqZFKwIDAQABoycwJTAOBgNVHQ8BAf8EBAMCBaAwEwYDVR0lBAwwCgYIKwYBBQUH
AwIwDQYJKoZIhvcNAQELBQADggEBAL9sKWHOj1cia9i+QZDg4UIFY3STEyH/FJXn
NknAH5u0ioW4y+OIjFMQDqNjTiIjVm2qnEf1K+MYgsb88OObUo84edVNvu8Fq2Fl
o/K3+OwMXjYjzlLIh+pUQp7KXiyt2TdCh06l/ApjPgAnVa2uIB+/SGndDB8VNXhb
i2YHnN60aOPJkq6ow00Y0s6ncjYtmeiUs8B6wZljGcbOEopXAiS9sDIYz1p4a0AB
5vbzZ8mh4fM+zQ5owUopVvBF4Mv8yJEHgJ/zoxwGT/7kmcmZ/27ix0v7kFvrhKaU
NOrsafukaeMnu7sKsM5jeCimps8GlBJUM6bVrlbAgUuPl5B0oWg=
-----END CERTIFICATE-----
";
            var x509 = TlsCertificate.FromPemParts(certPem).ToX509();

            Assert.Equal("CN=kubernetes", x509.Issuer);
            Assert.Equal("43EE9CFF1FBFBCCEA1A73C7F941F7921E2B688EF", x509.Thumbprint);
            Assert.Equal(new DateTime(2019, 2, 14, 15, 46, 05), x509.NotBefore);
            Assert.Equal(new DateTime(2020, 2, 14, 15, 46, 07), x509.NotAfter);

            Assert.False(x509.HasPrivateKey);
        }

        [Fact(Skip = "Enable for .NET Standard 2.0")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void X509_WithPrivateKey()
        {
            // $todo(jefflill):
            //
            // Enable this when we upgrade to .NET Standard 2.1
            //
            //      https://github.com/nforgeio/neonKUBE/issues/new

            // Verify that we can load a certificate generated by 
            // Kubernetes without a private key.

            var certPem =
@"-----BEGIN CERTIFICATE-----
MIIC8jCCAdqgAwIBAgIIdaABVSybOjowDQYJKoZIhvcNAQELBQAwFTETMBEGA1UE
AxMKa3ViZXJuZXRlczAeFw0xOTAyMTQyMzQ2MDVaFw0yMDAyMTQyMzQ2MDdaMDQx
FzAVBgNVBAoTDnN5c3RlbTptYXN0ZXJzMRkwFwYDVQQDExBrdWJlcm5ldGVzLWFk
bWluMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAzjgtmL1scH9zwMkF
syXmhuhchLDgkZ+CuEIwmACE6AwlHZ6XfEXaRnLUuwmv87S2epj9rwJ1Sp/EchYV
Q6YjOODYfkJXSxCTiiNwBCzGrTDqKa8JBj1Zy9jkC+ZoKoErJFRKlw5ePtxsdUZE
QozQpIFvjACrTcmJtJiaGnY2C/f6Csrf4OXT2ulSYq+AS3bY4cfrcXGdsUTG5ATS
DblHvntjqLi8loEEj2kjDbK82chAkcgPadQ28/P6cqwKnxHAgWeleCSxxNvHE8+O
vJiqrxh7TBUQiFWWSbgAVED8JiN++E779P/MgQCeGYPD3OgrW97T1U7ciQWZ38XB
KqZFKwIDAQABoycwJTAOBgNVHQ8BAf8EBAMCBaAwEwYDVR0lBAwwCgYIKwYBBQUH
AwIwDQYJKoZIhvcNAQELBQADggEBAL9sKWHOj1cia9i+QZDg4UIFY3STEyH/FJXn
NknAH5u0ioW4y+OIjFMQDqNjTiIjVm2qnEf1K+MYgsb88OObUo84edVNvu8Fq2Fl
o/K3+OwMXjYjzlLIh+pUQp7KXiyt2TdCh06l/ApjPgAnVa2uIB+/SGndDB8VNXhb
i2YHnN60aOPJkq6ow00Y0s6ncjYtmeiUs8B6wZljGcbOEopXAiS9sDIYz1p4a0AB
5vbzZ8mh4fM+zQ5owUopVvBF4Mv8yJEHgJ/zoxwGT/7kmcmZ/27ix0v7kFvrhKaU
NOrsafukaeMnu7sKsM5jeCimps8GlBJUM6bVrlbAgUuPl5B0oWg=
-----END CERTIFICATE-----
";
            var keyPem =
@"-----BEGIN RSA PRIVATE KEY-----
MIIEogIBAAKCAQEAzjgtmL1scH9zwMkFsyXmhuhchLDgkZ+CuEIwmACE6AwlHZ6X
fEXaRnLUuwmv87S2epj9rwJ1Sp/EchYVQ6YjOODYfkJXSxCTiiNwBCzGrTDqKa8J
Bj1Zy9jkC+ZoKoErJFRKlw5ePtxsdUZEQozQpIFvjACrTcmJtJiaGnY2C/f6Csrf
4OXT2ulSYq+AS3bY4cfrcXGdsUTG5ATSDblHvntjqLi8loEEj2kjDbK82chAkcgP
adQ28/P6cqwKnxHAgWeleCSxxNvHE8+OvJiqrxh7TBUQiFWWSbgAVED8JiN++E77
9P/MgQCeGYPD3OgrW97T1U7ciQWZ38XBKqZFKwIDAQABAoIBAErAOmb/Yut0h7T+
KT7DIkkMuVyv8PdYZr374Dl5FrQ2ks2lyyuU9oZK4ana3RjuDKdsBakGrxWZzE++
iX64HlRjzJYX3iSroY+VQOmCgZIOBROPCypj2sT1ndRidKfTopvMoi0XXDpVFEt+
aQfmm0rGUHTjWTUdNPltx46IAxda6Yd378hXAUmTQSQ2FoVcmHuQn4Sr/0Qehf8W
ZeyEzhD1s6w2KhAWZKtadvmeBV/rBpaK/iH+B9bKH+7afSJU6otSuNidE+u6m8gY
6Q4JSHa7v+vR3hLBlbIMoBd21vXNCpoTpgt7+kbE1j224aZ/TAYww5E4nHC16KBZ
c/HE/+ECgYEA47c5WhRq8B+yh/Bqj2AzGxYqSBtPmzIpFktpyDbRkxrVBELOb/Es
52D0KLoAc0rs4XtcxU94ayCUGBQbW7ITiIb19wqGhS+9ATifPyjB/1N9E4beEutO
y2sbsuTqjDsnGx+s1XDZqbx6K/cvGGSAEbNxEXJ0GSOLUMUaTQFwX2cCgYEA59Vt
froASB3KAgOroXCC6c3CVIbYu2pP9Am1aGFUITOUZB54elsY1is7wKbVxgYqvmAn
kHuO8SVLuZTk9BmrtNnBxLh4aJNpb5L7BVGwwNLrSxEwgSmudzT3v31QrZgYXFsA
YF9QymqIHbUMN6HzFht3nITGs5sTKgtYWCazRZ0CgYBL5kJDeBK8vpPvI38hEtt1
58loB1JdVDbFq5UymrL36TWfGfVc8nIZHQPEn1qPEyYpccjWK0rjyhQSgoEr6wr/
spxBH0z/D45b3deWYatnwxgpbgaPH8c/ng+5bPuQihbav5AIBHlITf4asWUNKFJX
lAvX2OJBjstcvJWrnRMreQKBgGDJwSH0Q6PYE/tNTv1ifLVh+uzRM3DjTKgE2aDP
aZFG+H/oHMJwf+kCObsPrBY1gujiOgJfI2lX+cpr+D5U7VPeyb/4iASY7p7vTS+G
UHXgWO2JKqfyH+2SxpBCoEkpQ5pjP7/8az1mxpcofAZJ7bPgGcrVwCNB7flSrTp4
RcYdAoGAM713rdsR/0NsbN16e8hnaDphmtwAeG2PuRhIeG4VfXG/bjZ8sUEu8Jwb
v/diFRFasFCXhaZGcKluXqYGb4V3CRwDQRBTTbdmnIiUFuP1rY6o02vcZcW7wy5T
UUHWzDpotXDXwAwuIxh71LVCBnQRPryVc6Ynx3YF8HD8in600zw=
-----END RSA PRIVATE KEY-----
";
            var x509 = TlsCertificate.FromPemParts(certPem, keyPem).ToX509();

            Assert.Equal("CN=kubernetes", x509.Issuer);
            Assert.Equal("43EE9CFF1FBFBCCEA1A73C7F941F7921E2B688EF", x509.Thumbprint);
            Assert.Equal(new DateTime(2019, 2, 14, 15, 46, 05), x509.NotBefore);
            Assert.Equal(new DateTime(2020, 2, 14, 15, 46, 07), x509.NotAfter);
            Assert.True(x509.HasPrivateKey);
        }
    }
}
