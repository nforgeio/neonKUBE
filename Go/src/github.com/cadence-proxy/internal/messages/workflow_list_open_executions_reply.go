//-----------------------------------------------------------------------------
// FILE:		workflow_list_open_executions_reply.go
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

	// WorkflowListOpenExecutionsReply is a WorkflowReply of MessageType
	// WorkflowListOpenExecutionsReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowListOpenExecutionsRequest
	WorkflowListOpenExecutionsReply struct {
		*WorkflowReply
	}
)

// NewWorkflowListOpenExecutionsReply is the default constructor for
// a WorkflowListOpenExecutionsReply
//
// returns *WorkflowListOpenExecutionsReply -> a pointer to a newly initialized
// WorkflowListOpenExecutionsReply in memory
func NewWorkflowListOpenExecutionsReply() *WorkflowListOpenExecutionsReply {
	reply := new(WorkflowListOpenExecutionsReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowListOpenExecutionsReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowListOpenExecutionsReply) Clone() IProxyMessage {
	workflowListOpenExecutionsReply := NewWorkflowListOpenExecutionsReply()
	var messageClone IProxyMessage = workflowListOpenExecutionsReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowListOpenExecutionsReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
