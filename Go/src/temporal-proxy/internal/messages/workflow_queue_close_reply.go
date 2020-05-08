//-----------------------------------------------------------------------------
// FILE:		workflow_queue_close_reply.go
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

	// WorkflowQueueCloseReply is a WorkflowReply of MessageType
	// WorkflowQueueCloseReply.  It holds a reference to a WorkflowReply
	// and is the reply type to a WorkflowQueueCloseRequest
	WorkflowQueueCloseReply struct {
		*WorkflowReply
	}
)

// NewWorkflowQueueCloseReply is the default constructor for
// a WorkflowQueueCloseReply
//
// returns *WorkflowQueueCloseReply -> a pointer to a newly initialized
// WorkflowQueueCloseReply.
func NewWorkflowQueueCloseReply() *WorkflowQueueCloseReply {
	reply := new(WorkflowQueueCloseReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowQueueCloseReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowQueueCloseReply) Build(e *proxyerror.TemporalError, result ...interface{}) {
	reply.WorkflowReply.Build(e)
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowQueueCloseReply) Clone() IProxyMessage {
	workflowQueueCloseReply := NewWorkflowQueueCloseReply()
	var messageClone IProxyMessage = workflowQueueCloseReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowQueueCloseReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
