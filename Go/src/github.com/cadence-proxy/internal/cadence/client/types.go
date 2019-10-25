//-----------------------------------------------------------------------------
// FILE:		types.go
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

import (
	"strings"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
)

// StringToTaskListType takes a valid TaskListType
// as a string and converts it into a TaskListType
func StringToTaskListType(value string) *cadenceshared.TaskListType {
	value = strings.ToUpper(value)
	switch value {
	case "DECISION":
		return cadenceshared.TaskListTypeDecision.Ptr()
	case "ACTIVITY":
		return cadenceshared.TaskListTypeActivity.Ptr()
	default:
		return nil
	}
}

// StringToTaskListKind takes a valid TaskListKind
// as a string and converts it into a TaskListKind
func StringToTaskListKind(value string) *cadenceshared.TaskListKind {
	value = strings.ToUpper(value)
	switch value {
	case "NORMAL":
		return cadenceshared.TaskListKindNormal.Ptr()
	case "STICKY":
		return cadenceshared.TaskListKindSticky.Ptr()
	default:
		return nil
	}
}

// StringToDomainStatus takes a valid domain status
// as a string and converts it into a domain status
// if possible
func StringToDomainStatus(value string) *cadenceshared.DomainStatus {
	value = strings.ToUpper(value)
	switch value {
	case "REGISTERED":
		return cadenceshared.DomainStatusRegistered.Ptr()
	case "DEPRECATED":
		return cadenceshared.DomainStatusDeprecated.Ptr()
	case "DELETED":
		return cadenceshared.DomainStatusDeleted.Ptr()
	default:
		return nil
	}
}
