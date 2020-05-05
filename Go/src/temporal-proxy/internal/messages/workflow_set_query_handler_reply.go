//-----------------------------------------------------------------------------
// FILE:		workflow_set_query_handler_reply.go
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

	// WorkflowSetQueryHandlerReply is a WorkflowReply of MessageType
	// WorkflowSetQueryHandlerReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSetQueryHandlerRequest
	WorkflowSetQueryHandlerReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSetQueryHandlerReply is the default constructor for
// a WorkflowSetQueryHandlerReply
//
// returns *WorkflowSetQueryHandlerReply -> a pointer to a newly initialized
// WorkflowSetQueryHandlerReply in memory
func NewWorkflowSetQueryHandlerReply() *WorkflowSetQueryHandlerReply {
	reply := new(WorkflowSetQueryHandlerReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowSetQueryHandlerReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSetQueryHandlerReply) Clone() IProxyMessage {
	workflowSetQueryHandlerReply := NewWorkflowSetQueryHandlerReply()
	var messageClone IProxyMessage = workflowSetQueryHandlerReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSetQueryHandlerReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
