//-----------------------------------------------------------------------------
// FILE:		workflow_signal_child_reply.go
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

	// WorkflowSignalChildReply is a WorkflowReply of MessageType
	// WorkflowSignalChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalChildRequest
	WorkflowSignalChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalChildReply is the default constructor for
// a WorkflowSignalChildReply
//
// returns *WorkflowSignalChildReply -> a pointer to a newly initialized
// WorkflowSignalChildReply in memory
func NewWorkflowSignalChildReply() *WorkflowSignalChildReply {
	reply := new(WorkflowSignalChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowSignalChildReply)

	return reply
}

// GetResult gets the child workflow signal result or nil
// from a WorkflowSignalChildReply's properties map.
//
// returns []byte -> []byte representing the result of a child workflow signal
func (reply *WorkflowSignalChildReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the child workflow signal result or nil
// in a WorkflowSignalChildReply's properties map.
//
// param value []byte -> []byte representing the result of a child workflow signal
// to be set in the WorkflowSignalChildReply's properties map
func (reply *WorkflowSignalChildReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowSignalChildReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalChildReply) Clone() IProxyMessage {
	workflowSignalChildReply := NewWorkflowSignalChildReply()
	var messageClone IProxyMessage = workflowSignalChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowSignalChildReply); ok {
		v.SetResult(reply.GetResult())
	}
}
