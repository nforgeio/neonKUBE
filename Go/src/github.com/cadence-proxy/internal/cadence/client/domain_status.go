//-----------------------------------------------------------------------------
// FILE:		domain_status.go
// CONTRIBUTOR: John C Burns
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

package proxyclient

// DomainStatus is an enumerated list of
// all of the valid cadence domain statuses
type DomainStatus int

const (

	// DomainStatusUnspecified indicates that no DomainStatus
	// has been set (specified)
	DomainStatusUnspecified DomainStatus = 0

	// DomainStatusRegistered indicates that the domain is registered and active.
	DomainStatusRegistered DomainStatus = 1

	// DomainStatusDeprecated indicates that the domain is closed for new workflows
	// but will remain until already running workflows are completed and the
	// history retention period for the last executed workflow
	// has been satisified.
	DomainStatusDeprecated DomainStatus = 2

	// DomainStatusDeleted indicates that a cadence domain has been deleted
	DomainStatusDeleted DomainStatus = 3
)

func (t DomainStatus) String() string {
	switch t {
	case DomainStatusRegistered:
		return "REGISTERED"
	case DomainStatusDeprecated:
		return "DEPRECATED"
	case DomainStatusDeleted:
		return "DELETED"
	default:
		return "UNSPECIFIED"
	}
}

// StringToDomainStatus takes a valid domain status
// as a string and converts it into a domain status
// if possible
func StringToDomainStatus(value string) DomainStatus {
	switch value {
	case "REGISTERED":
		return DomainStatusRegistered
	case "DEPRECATED":
		return DomainStatusDeprecated
	case "DELETED":
		return DomainStatusDeleted
	default:
		return DomainStatusUnspecified
	}
}
