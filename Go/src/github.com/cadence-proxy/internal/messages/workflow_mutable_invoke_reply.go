//-----------------------------------------------------------------------------
// FILE:		workflow_mutable_invoke_reply.go
// CONTRIBUTOR: John C Burnes
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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowMutableInvokeReply is a WorkflowReply of MessageType
	// WorkflowMutableInvokeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowMutableInvokeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowMutableInvokeReply is the default constructor for
// a WorkflowMutableInvokeReply
//
// returns *WorkflowMutableInvokeReply -> a pointer to a newly initialized
// WorkflowMutableInvokeReply in memory
func NewWorkflowMutableInvokeReply() *WorkflowMutableInvokeReply {
	reply := new(WorkflowMutableInvokeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowMutableInvokeReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowMutableInvokeReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowMutableInvokeReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowMutableInvokeReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowMutableInvokeReply's properties map
func (reply *WorkflowMutableInvokeReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowMutableInvokeReply) Clone() IProxyMessage {
	workflowMutableInvokeReply := NewWorkflowMutableInvokeReply()
	var messageClone IProxyMessage = workflowMutableInvokeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowMutableInvokeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowMutableInvokeReply); ok {
		v.SetResult(reply.GetResult())
	}
}
