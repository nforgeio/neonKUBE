//-----------------------------------------------------------------------------
// FILE:		workflow_signal_child_reply.go
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

	// WorkflowSignalChildReply is a WorkflowReply of MessageType
	// WorkflowSignalChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalChildRequest
	WorkflowSignalChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalChildReply is the default constructor for
// a WorkflowSignalChildReply
//
// returns *WorkflowSignalChildReply -> a pointer to a newly initialized
// WorkflowSignalChildReply in memory
func NewWorkflowSignalChildReply() *WorkflowSignalChildReply {
	reply := new(WorkflowSignalChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSignalChildReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalChildReply) Clone() IProxyMessage {
	workflowSignalChildReply := NewWorkflowSignalChildReply()
	var messageClone IProxyMessage = workflowSignalChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
