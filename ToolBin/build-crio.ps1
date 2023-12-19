#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build-crio.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# Performs a clean build of the NEONKUBE solution and publishes binaries
# to the [$/build] folder.  This can also optionally build the NEONKUBE
# Desktop installer.
#
# USAGE: pwsh -file ./build-crio.ps1 VERSION [-publish]
#
# ARGUMENTS:
#
#       VERSION     - the CRI-O version (without the leading "v")
#
# OPTIONS:
#
#       -publish    - publish the binary to S3

param 
(
    [parameter(Mandatory=$true, Position=1)][string]$version,
    [parameter(Mandatory=$true, Position=2)][string]$outputFolder
    [switch]$publish
)

if ($version.StartsWith("v"))
{
    $version = $version.SubString(1)
}

$buildScript = 
@"
# This script builds a specific tagged version of CRI-O from source.
#
#	https://github.com/cri-o/cri-o/blob/main/install.md#debian-bullseye-or-higher---ubuntu-2004-or-higher

set -euo pipefail

export CRIO_VERSION=$<CRIO-VERSION>
export GO_VERSION=1.20

# Install the package dependencies.

apt-get update
apt-get update -qq && apt-get install -y \
  libbtrfs-dev \
  containers-common \
  git \
  golang-go \
  libassuan-dev \
  libdevmapper-dev \
  libglib2.0-dev \
  libc6-dev \
  libgpgme-dev \
  libgpg-error-dev \
  libseccomp-dev \
  libsystemd-dev \
  libselinux1-dev \
  pkg-config \
  go-md2man \
  cri-o-runc \
  libudev-dev \
  software-properties-common \
  gcc \
  make
  
# Install the required version of GOLANG.
  
curl -4fsSL --retry 10 --retry-delay 30 https://go.dev/dl/go1.${GO_VERSION}.linux-amd64.tar.gz -o /tmp/golang.tar.gz
rm -rf /usr/local/go 
tar -C /usr/local -xzf /tmp/golang.tar.gz
rm /tmp/golang.tar.gz
export PATH=/usr/local/go/bin:$PATH

# Clone CRI-O to [/build/cri-o] and checkout the tagged release.

mkdir -p /build
cd /build
rm -rf cri-o
git clone https://github.com/cri-o/cri-o cri-o
cd cri-o
git checkout tags/v$CRIO_VERSION > /dev/nul

# Build CRI-O 

make

# Compress the binary to: /build/cri-o/bin/crio.gz

gzip /build/cri-o/bin/crio --keep
"@

$buildScript = $buildScript.Replace("$<CRIO-VERSION>", $version)
