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
	"fmt"
	"strings"

	namespace "go.temporal.io/temporal-proto/namespace"
	tasklist "go.temporal.io/temporal-proto/tasklist"
)

// StringToTaskListType takes a valid TaskListType
// as a string and converts it into a TaskListType
func StringToTaskListType(value string) tasklist.TaskListType {
	value = strings.ToUpper(value)
	switch value {
	case "DECISION":
		return tasklist.TaskListType_Decision
	case "ACTIVITY":
		return tasklist.TaskListType_Activity
	default:
		panic(fmt.Errorf("Invalid Tasklist Type string: %s", value))
	}
}

// StringToTaskListKind takes a valid TaskListKind
// as a string and converts it into a TaskListKind
func StringToTaskListKind(value string) tasklist.TaskListKind {
	value = strings.ToUpper(value)
	switch value {
	case "NORMAL":
		return tasklist.TaskListKind_Normal
	case "STICKY":
		return tasklist.TaskListKind_Sticky
	default:
		panic(fmt.Errorf("Invalid Tasklist Kind string: %s", value))
	}
}

// StringToNamespaceStatus takes a valid domain status
// as a string and converts it into a domain status
// if possible
func StringToNamespaceStatus(value string) namespace.NamespaceStatus {
	value = strings.ToUpper(value)
	switch value {
	case "REGISTERED":
		return namespace.NamespaceStatus_Registered
	case "DEPRECATED":
		return namespace.NamespaceStatus_Deprecated
	case "DELETED":
		return namespace.NamespaceStatus_Deleted
	default:
		panic(fmt.Errorf("Invalid Namespace Status string: %s", value))
	}
}
