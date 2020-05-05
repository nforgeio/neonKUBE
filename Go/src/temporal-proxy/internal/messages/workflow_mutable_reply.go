//-----------------------------------------------------------------------------
// FILE:		workflow_mutable_reply.go
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

	// WorkflowMutableReply is a WorkflowReply of MessageType
	// WorkflowMutableReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowMutableReply struct {
		*WorkflowReply
	}
)

// NewWorkflowMutableReply is the default constructor for
// a WorkflowMutableReply
//
// returns *WorkflowMutableReply -> a pointer to a newly initialized
// WorkflowMutableReply in memory
func NewWorkflowMutableReply() *WorkflowMutableReply {
	reply := new(WorkflowMutableReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowMutableReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowMutableReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowMutableReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowMutableReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowMutableReply's properties map
func (reply *WorkflowMutableReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowMutableReply) Clone() IProxyMessage {
	workflowMutableReply := NewWorkflowMutableReply()
	var messageClone IProxyMessage = workflowMutableReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowMutableReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowMutableReply); ok {
		v.SetResult(reply.GetResult())
	}
}
