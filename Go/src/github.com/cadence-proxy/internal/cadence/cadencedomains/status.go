//-----------------------------------------------------------------------------
// FILE:		status.go
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

package domain

import "fmt"

// DomainStatus is an enumerated list of
// all of the valid cadence domain statuses
type DomainStatus int

const (

	// Registered indicates that the domain is registered and active.
	Registered DomainStatus = 0

	// Deprecated indicates that the domain is closed for new workflows
	// but will remain until already running workflows are completed and the
	// history retention period for the last executed workflow
	// has been satisified.
	Deprecated DomainStatus = 1

	// Deleted indicates that a cadence domain has been deleted
	Deleted DomainStatus = 2
)

func (t DomainStatus) String() string {
	return [...]string{
		"REGISTERED",
		"DEPRECATED",
		"DELETED",
	}[t]
}

func StringToDomainStatus(value string) DomainStatus {
	switch value {
	case "REGISTERED":
		return Registered
	case "DEPRECATED":
		return Deprecated
	case "DELETED":
		return Deleted
	default:
		err := fmt.Errorf("unknown string value %s for %s", value, "DomainStatus")
		panic(err)
	}
}
