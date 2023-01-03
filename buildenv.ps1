#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         buildenv.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

#------------------------------------------------------------------------------
# This script handles additional build environment configuration for the solution.

#------------------------------------------------------------------------------
# Open Windows Firewall ports required for unit testing.

# Remove unnecessary rules.

Remove-NetFirewallRule -Name "Inbound-UnitTesting" -EA Silent | Out-Null

# Add update current rules.

Remove-NetFirewallRule -Name "Inbound-UnitTesting-TCP" -EA Silent | Out-Null

New-NetFirewallRule -Name "Inbound-UnitTesting-TCP" `
                    -DisplayName "[TEST] allow inbound TCP ports 1-65535" `
                    -Direction Inbound `
                    -Action Allow `
                    -LocalPort 1-65535 `
                    -Protocol TCP `
                    -Profile Any `
                    -Description "Open TCP ports for unit testing" | Out-Null

Remove-NetFirewallRule -Name "Inbound-UnitTesting-UDP" -EA Silent | Out-Null

New-NetFirewallRule -Name "Inbound-UnitTesting-UDP" `
                    -DisplayName "[TEST] allow inbound UDP ports 1-65535" `
                    -Direction Inbound `
                    -Action Allow `
                    -LocalPort 1-65535 `
                    -Protocol UDP `
                    -Profile Any `
                    -Description "Open UDP ports for unit testing" | Out-Null

#------------------------------------------------------------------------------
# Install additional Powershell modules.

Install-Module powershell-yaml -Force
