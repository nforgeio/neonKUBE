//-----------------------------------------------------------------------------
// FILE:		workflow_cancel_child_reply.go
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
	internal "temporal-proxy/internal"
)

type (

	// WorkflowCancelChildReply is a WorkflowReply of MessageType
	// WorkflowCancelChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowCancelChildRequest
	WorkflowCancelChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowCancelChildReply is the default constructor for
// a WorkflowCancelChildReply
//
// returns *WorkflowCancelChildReply -> a pointer to a newly initialized
// WorkflowCancelChildReply in memory
func NewWorkflowCancelChildReply() *WorkflowCancelChildReply {
	reply := new(WorkflowCancelChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowCancelChildReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowCancelChildReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowCancelChildReply) Clone() IProxyMessage {
	workflowCancelChildReply := NewWorkflowCancelChildReply()
	var messageClone IProxyMessage = workflowCancelChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowCancelChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
