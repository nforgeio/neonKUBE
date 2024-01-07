#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
#
# The contents of this repository are for private use by NEONFORGE, LLC. and may not be
# divulged or used for any purpose by other organizations or individuals without a
# formal written and signed agreement with NEONFORGE, LLC.

param 
(
	[parameter(Mandatory=$true, Position=1)][string] $registry,
	[parameter(Mandatory=$true, Position=2)][string] $version,
	[parameter(Mandatory=$true, Position=3)][string] $tag
)

Pull-ContainerImage "registry.k8s.io/coredns/coredns:$version"
Invoke-CaptureStreams "docker build -t ${registry}:$tag --build-arg VERSION=$version ." -interleave | Out-Null
