//-----------------------------------------------------------------------------
// FILE:		workflow_query_reply.go
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

	// WorkflowQueryReply is a WorkflowReply of MessageType
	// WorkflowQueryReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowQueryReply struct {
		*WorkflowReply
	}
)

// NewWorkflowQueryReply is the default constructor for
// a WorkflowQueryReply
//
// returns *WorkflowQueryReply -> a pointer to a newly initialized
// WorkflowQueryReply in memory
func NewWorkflowQueryReply() *WorkflowQueryReply {
	reply := new(WorkflowQueryReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowQueryReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowQueryReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowQueryReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowQueryReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowQueryReply's properties map
func (reply *WorkflowQueryReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowQueryReply) Clone() IProxyMessage {
	workflowQueryReply := NewWorkflowQueryReply()
	var messageClone IProxyMessage = workflowQueryReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowQueryReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowQueryReply); ok {
		v.SetResult(reply.GetResult())
	}
}
