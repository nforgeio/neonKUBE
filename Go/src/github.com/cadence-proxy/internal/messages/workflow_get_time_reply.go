//-----------------------------------------------------------------------------
// FILE:		workflow_get_time_reply.go
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

package messages

import (
	"time"

	internal "github.com/cadence-proxy/internal"
)

type (

	// WorkflowGetTimeReply is a WorkflowReply of MessageType
	// WorkflowGetTimeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowGetTimeRequest
	WorkflowGetTimeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowGetTimeReply is the default constructor for
// a WorkflowGetTimeReply
//
// returns *WorkflowGetTimeReply -> a pointer to a newly initialized
// WorkflowGetTimeReply in memory
func NewWorkflowGetTimeReply() *WorkflowGetTimeReply {
	reply := new(WorkflowGetTimeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowGetTimeReply)

	return reply
}

// GetTime gets the Time property from the WorkflowGetTimeReply's
// properties map. Time is the current workflow time expressed as
// 100 nanosecond ticks since 01/01/0001 00:00.
//
// returns time.Time -> the value of the Time property from
// the WorkflowGetTimeReply's properties map.
func (reply *WorkflowGetTimeReply) GetTime() time.Time {
	return reply.GetDateTimeProperty("Time")
}

// SetTime sets the Time property in the WorkflowGetTimeReply's
// properties map. Time is the current workflow time expressed as
// 100 nanosecond ticks since 01/01/0001 00:00.
//
// param value time.Time -> the Time to be set in the
// WorkflowGetTimeReply's properties map.
func (reply *WorkflowGetTimeReply) SetTime(value time.Time) {
	reply.SetDateTimeProperty("Time", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowGetTimeReply) Clone() IProxyMessage {
	workflowGetTimeReply := NewWorkflowGetTimeReply()
	var messageClone IProxyMessage = workflowGetTimeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowGetTimeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowGetTimeReply); ok {
		v.SetTime(reply.GetTime())
	}
}
