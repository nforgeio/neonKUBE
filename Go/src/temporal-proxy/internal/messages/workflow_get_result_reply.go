//-----------------------------------------------------------------------------
// FILE:		workflow_get_result_reply.go
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

	// WorkflowGetResultReply is a WorkflowReply of MessageType
	// WorkflowGetResultReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowGetResultRequest
	WorkflowGetResultReply struct {
		*WorkflowReply
	}
)

// NewWorkflowGetResultReply is the default constructor for
// a WorkflowGetResultReply
//
// returns *WorkflowGetResultReply -> a pointer to a newly initialized
// WorkflowGetResultReply in memory
func NewWorkflowGetResultReply() *WorkflowGetResultReply {
	reply := new(WorkflowGetResultReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowGetResultReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowGetResultReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowGetResultReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowGetResultReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowGetResultReply's properties map
func (reply *WorkflowGetResultReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowGetResultReply) Clone() IProxyMessage {
	workflowGetResultReply := NewWorkflowGetResultReply()
	var messageClone IProxyMessage = workflowGetResultReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowGetResultReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowGetResultReply); ok {
		v.SetResult(reply.GetResult())
	}
}
