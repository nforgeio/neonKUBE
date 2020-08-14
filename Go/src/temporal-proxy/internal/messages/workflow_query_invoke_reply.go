//-----------------------------------------------------------------------------
// FILE:		workflow_query_invoke_reply.go
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

	// WorkflowQueryInvokeReply is a WorkflowReply of MessageType
	// WorkflowQueryInvokeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowQueryInvokeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowQueryInvokeReply is the default constructor for
// a WorkflowQueryInvokeReply
//
// returns *WorkflowQueryInvokeReply -> a pointer to a newly initialized
// WorkflowQueryInvokeReply in memory
func NewWorkflowQueryInvokeReply() *WorkflowQueryInvokeReply {
	reply := new(WorkflowQueryInvokeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowQueryInvokeReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowQueryInvokeReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowQueryInvokeReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowQueryInvokeReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowQueryInvokeReply's properties map
func (reply *WorkflowQueryInvokeReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowQueryInvokeReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowQueryInvokeReply) Clone() IProxyMessage {
	workflowQueryInvokeReply := NewWorkflowQueryInvokeReply()
	var messageClone IProxyMessage = workflowQueryInvokeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowQueryInvokeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowQueryInvokeReply); ok {
		v.SetResult(reply.GetResult())
	}
}
