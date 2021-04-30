#Requires -Version 7.0
#------------------------------------------------------------------------------
# FILE:         buildenv.ps1
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

# Additional environment initialization.

# Open Windows Firewall ports required for unit testing.

Remove-NetFirewallRule -Name "Inbound-Cadence" -EA Silent | Out-Null

New-NetFirewallRule -Name "Inbound-Cadence" `
                    -DisplayName "[TEST] allow inbound Cadence" `
                    -Direction Inbound `
                    -Action Allow `
                    -LocalPort 7933 `
                    -Protocol TCP `
                    -Profile Any `
                    -Description "Allow external connections to Cadence servers" | Out-Null

Remove-NetFirewallRule -Name "Inbound-Temporal" -EA Silent | Out-Null

New-NetFirewallRule -Name "Inbound-Temporal" `
                    -DisplayName "[TEST] allow inbound Temporal" `
                    -Direction Inbound `
                    -Action Allow `
                    -LocalPort 7233 `
                    -Protocol TCP `
                    -Profile Any `
                    -Description "Allow external connections to Temporal servers" | Out-Null
