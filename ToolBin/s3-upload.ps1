#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         s3-upload.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

# Uploads a file to S3 using maintainer credentials managed by [neon-assistant].
#
# ARGUMENTS:
#
#   sourcePath          - path to the file being uploaded
#
#   targetUri           - the target S3 URI This may be either an [s3://BUCKET/KEY] or a
#                         https://s3.REGION.amazonaws.com/BUCKET/KEY URI referencing an S3 
#                         bucket and key.
#
#   gzip                - Optionally indicates that the target content encoding should be set to [gzip]
#
#   metadata            - Optionally specifies HTTP metadata headers to be returned when the object
#                         is downloaded from S3.  This formatted as as comma separated a list of 
#                         key/value pairs like:
#        
#                               Content-Type=text,app-version=1.0.0
#
#                         AWS supports [system] as well as [custom] headers.  System headers
#                         include standard HTTP headers such as [Content-Type] and [Content-Encoding].
#                         Custom headers are required to include the <b>x-amz-meta-</b> prefix.
#
#                         You don't need to specify the [x-amz-meta-] prefix for setting custom 
#                         headers; the AWS-CLI detects custom header names and adds the prefix automatically. 
#                         This method will strip the prefix if present before calling the AWS-CLI to ensure 
#                         the prefix doesn't end up being duplicated.
#
#   publicReadAccess    - Optionally indicates that the upload file will allow read-only access to the world.

param 
(
    [Parameter(Position=0, Mandatory=$true)]
    [string]$sourcePath,
    [Parameter(Position=1, Mandatory=$true)]
    [string]$targetUri,
    [Parameter(Mandatory=$false)]
    [switch]$gzip = $false,
    [Parameter(Mandatory=$false)]
    [string]$metadata = "",
    [Parameter(Mandatory=$false)]
    [switch]$publicReadAccess = $false
)

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Initisalize the AWS credentisls.

Import-AwsCliCredentials

# Upload the file.

Save-Tos3 $sourcePath $targetUri -gzip $gzip -metadata $metadata -publicReadAccess $publicReadAccess
