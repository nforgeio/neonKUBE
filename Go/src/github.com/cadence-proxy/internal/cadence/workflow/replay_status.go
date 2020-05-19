//-----------------------------------------------------------------------------
// FILE:		replay_status.go
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

package workflow

// ReplayStatus indicates a workflow's
// current replay status.
type ReplayStatus int

const (

	// ReplayStatusUnspecified indicates that the corresponding
	// operation cannot determine the replay
	// status (e.g. because the it didn't relate
	// to an executing workflow).
	// This is the default value.
	ReplayStatusUnspecified ReplayStatus = 0

	// ReplayStatusNotReplaying indicates that
	// the related workflow is not replaying.
	ReplayStatusNotReplaying ReplayStatus = 1

	// ReplayStatusReplaying indicates that
	// The related workflow is replaying.
	ReplayStatusReplaying ReplayStatus = 2
)

// String is called on a ReplayStatus
// instance and returns its string value
func (r ReplayStatus) String() string {
	switch r {
	case ReplayStatusNotReplaying:
		return "NotReplaying"
	case ReplayStatusReplaying:
		return "Replaying"
	default:
		return "Unspecified"
	}
}

// StringToReplayStatus takes a string and tries to
// match it with a ReplayStatus, returning a string
// if it can
func StringToReplayStatus(value string) ReplayStatus {
	switch value {
	case "NotReplaying":
		return ReplayStatusNotReplaying
	case "Replaying":
		return ReplayStatusReplaying
	default:
		return ReplayStatusUnspecified
	}
}
