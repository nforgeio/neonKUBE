//-----------------------------------------------------------------------------
// FILE:		workflow_sleep_reply.go
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
	internal "github.com/cadence-proxy/internal"
)

type (

	// WorkflowSleepReply is a WorkflowReply of MessageType
	// WorkflowSleepReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSleepRequest
	WorkflowSleepReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSleepReply is the default constructor for
// a WorkflowSleepReply
//
// returns *WorkflowSleepReply -> a pointer to a newly initialized
// WorkflowSleepReply in memory
func NewWorkflowSleepReply() *WorkflowSleepReply {
	reply := new(WorkflowSleepReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowSleepReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSleepReply) Clone() IProxyMessage {
	workflowSleepReply := NewWorkflowSleepReply()
	var messageClone IProxyMessage = workflowSleepReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSleepReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
