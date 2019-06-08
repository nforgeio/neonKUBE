//-----------------------------------------------------------------------------
// FILE:		workflow_set_signal_handler_reply.go
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
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetSignalHandlerReply is a WorkflowReply of MessageType
	// WorkflowSetSignalHandlerReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSetSignalHandlerRequest
	WorkflowSetSignalHandlerReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSetSignalHandlerReply is the default constructor for
// a WorkflowSetSignalHandlerReply
//
// returns *WorkflowSetSignalHandlerReply -> a pointer to a newly initialized
// WorkflowSetSignalHandlerReply in memory
func NewWorkflowSetSignalHandlerReply() *WorkflowSetSignalHandlerReply {
	reply := new(WorkflowSetSignalHandlerReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSetSignalHandlerReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSetSignalHandlerReply) Clone() IProxyMessage {
	workflowSetSignalHandlerReply := NewWorkflowSetSignalHandlerReply()
	var messageClone IProxyMessage = workflowSetSignalHandlerReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSetSignalHandlerReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
