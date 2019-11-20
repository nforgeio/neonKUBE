SSH.NET
=======
SSH.NET is a Secure Shell (SSH-2) library for .NET, optimized for parallelism.

For information on how to use this, please visit the original repository here:

&nbps;&nbps;&nbps;&nbps;[sshnet/SSH.NET](https://github.com/sshnet/SSH.NET)

# Project Goals

SSH.NET was originally maintained at [sshnet/SSH.NET](https://github.com/sshnet/SSH.NET) but support there has waned for the past couple of years with the last NuGet package being released in 10-2017 and the last commit on the **develop** branch occuring in 07-1018.

The original SSH.NET Nuget packages were working well for us up to 02-2019, when Linux OpenSSH fixed this security vulnerability [CVE-2018-20685](http://changelogs.ubuntu.com/changelogs/pool/main/o/openssh/openssh_7.2p2-4ubuntu2.7/changelog) which breaks SSH.NET file uploads.  Here are some tracking issues:

[#515: Bugfix for "scp: error: unexpected filename](https://github.com/sshnet/SSH.NET/pull/515)
[#450: OpenSSL fix breaks SSH.NET upload](https://github.com/nforgeio/neonKUBE/issues/450)

neonFORGE required a fix for this issue to support the neonKUBE Kubernetes distribution so we went ahead and cloned the project and applied some fixes suggested but not commited to the original repo.

We'll be publishing this to Nuget as **Neon.SSH.NET** as a .NETStandard 2.0 class library for our own purposes, but the community is welcome to use this under the orignal [MIT](https://opensource.org/licenses/MIT) as well as the [Apache v2](https://opensource.org/licenses/Apache-2.0) licenses.

**IMPORTANT NOTE:** Our primary goal here to solve our own problems and we hope and expect to do only very limited upgrades to this library over time.  We are by no means experts on the SSH/SCP protocols nor this codebase. 

## Repistory Clone Information

This directory is a partial copy of the [sshnet/SSH.NET](https://github.com/sshnet/SSH.NET) GitHub repository.  This was copied on **06-04-2019** from the **develop** branch at commit **bd01d97**.