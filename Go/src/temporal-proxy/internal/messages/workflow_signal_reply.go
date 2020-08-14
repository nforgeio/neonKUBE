//-----------------------------------------------------------------------------
// FILE:		workflow_signal_reply.go
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

	// WorkflowSignalReply is a WorkflowReply of MessageType
	// WorkflowSignalReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalRequest
	WorkflowSignalReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalReply is the default constructor for
// a WorkflowSignalReply
//
// returns *WorkflowSignalReply -> a pointer to a newly initialized
// WorkflowSignalReply in memory
func NewWorkflowSignalReply() *WorkflowSignalReply {
	reply := new(WorkflowSignalReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowSignalReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowSignalReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalReply) Clone() IProxyMessage {
	workflowSignalReply := NewWorkflowSignalReply()
	var messageClone IProxyMessage = workflowSignalReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
