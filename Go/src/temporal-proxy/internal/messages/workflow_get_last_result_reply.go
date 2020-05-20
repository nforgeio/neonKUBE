//-----------------------------------------------------------------------------
// FILE:		workflow_get_last_result_reply.go
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
	proxyerror "temporal-proxy/internal/temporal/error"
)

type (

	// WorkflowGetLastResultReply is a WorkflowReply of MessageType
	// WorkflowGetLastResultReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowGetLastResultReply struct {
		*WorkflowReply
	}
)

// NewWorkflowGetLastResultReply is the default constructor for
// a WorkflowGetLastResultReply
//
// returns *WorkflowGetLastResultReply -> a pointer to a newly initialized
// WorkflowGetLastResultReply in memory
func NewWorkflowGetLastResultReply() *WorkflowGetLastResultReply {
	reply := new(WorkflowGetLastResultReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowGetLastResultReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowGetLastResultReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowGetLastResultReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowGetLastResultReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowGetLastResultReply's properties map
func (reply *WorkflowGetLastResultReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowGetLastResultReply) Build(e *proxyerror.TemporalError, result ...interface{}) {
	reply.WorkflowReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowGetLastResultReply) Clone() IProxyMessage {
	workflowGetLastResultReply := NewWorkflowGetLastResultReply()
	var messageClone IProxyMessage = workflowGetLastResultReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowGetLastResultReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowGetLastResultReply); ok {
		v.SetResult(reply.GetResult())
	}
}
