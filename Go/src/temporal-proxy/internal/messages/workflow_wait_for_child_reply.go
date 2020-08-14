//-----------------------------------------------------------------------------
// FILE:		workflow_wait_for_child_reply.go
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

	// WorkflowWaitForChildReply is a WorkflowReply of MessageType
	// WorkflowWaitForChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowWaitForChildRequest
	WorkflowWaitForChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowWaitForChildReply is the default constructor for
// a WorkflowWaitForChildReply
//
// returns *WorkflowWaitForChildReply -> a pointer to a newly initialized
// WorkflowWaitForChildReply in memory
func NewWorkflowWaitForChildReply() *WorkflowWaitForChildReply {
	reply := new(WorkflowWaitForChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowWaitForChildReply)

	return reply
}

// GetResult gets the child workflow results encoded as bytes
// from a WorkflowWaitForChildReply's properties map.
//
// returns []byte -> []byte representing the result of a child workflow
func (reply *WorkflowWaitForChildReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the child workflow results encoded as bytes
// in a WorkflowWaitForChildReply's properties map.
//
// param value []byte -> []byte representing the result of a child workflow
// to be set in the WorkflowWaitForChildReply's properties map
func (reply *WorkflowWaitForChildReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowWaitForChildReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowWaitForChildReply) Clone() IProxyMessage {
	workflowWaitForChildReply := NewWorkflowWaitForChildReply()
	var messageClone IProxyMessage = workflowWaitForChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowWaitForChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowWaitForChildReply); ok {
		v.SetResult(reply.GetResult())
	}
}
