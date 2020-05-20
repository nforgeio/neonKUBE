//-----------------------------------------------------------------------------
// FILE:		workflow_terminate_reply.go
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

	// WorkflowTerminateReply is a WorkflowReply of MessageType
	// WorkflowTerminateReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowTerminateRequest
	WorkflowTerminateReply struct {
		*WorkflowReply
	}
)

// NewWorkflowTerminateReply is the default constructor for
// a WorkflowTerminateReply
//
// returns *WorkflowTerminateReply -> a pointer to a newly initialized
// WorkflowTerminateReply in memory
func NewWorkflowTerminateReply() *WorkflowTerminateReply {
	reply := new(WorkflowTerminateReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowTerminateReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowTerminateReply) Build(e *proxyerror.TemporalError, result ...interface{}) {
	reply.WorkflowReply.Build(e)
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowTerminateReply) Clone() IProxyMessage {
	workflowTerminateReply := NewWorkflowTerminateReply()
	var messageClone IProxyMessage = workflowTerminateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowTerminateReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
