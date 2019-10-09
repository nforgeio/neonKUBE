//-----------------------------------------------------------------------------
// FILE:		workflow_has_last_result_reply.go
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

	// WorkflowHasLastResultReply is a WorkflowReply of MessageType
	// WorkflowHasLastResultReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowHasLastResultRequest
	WorkflowHasLastResultReply struct {
		*WorkflowReply
	}
)

// NewWorkflowHasLastResultReply is the default constructor for
// a WorkflowHasLastResultReply
//
// returns *WorkflowHasLastResultReply -> a pointer to a newly initialized
// WorkflowHasLastResultReply in memory
func NewWorkflowHasLastResultReply() *WorkflowHasLastResultReply {
	reply := new(WorkflowHasLastResultReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowHasLastResultReply)

	return reply
}

// GetHasResult gets the HasResult property
// from a WorkflowHasLastResultReply's properties map.
// Indicates whether the workflow has a last completion result.
//
// returns bool -> HasResult from the WorkflowHasLastResultReply's
// properties map
func (reply *WorkflowHasLastResultReply) GetHasResult() bool {
	return reply.GetBoolProperty("HasResult")
}

// SetHasResult sets gets the HasResult property
// in a WorkflowHasLastResultReply's properties map.
// Indicates whether the workflow has a last completion result.
//
// param value bool -> HasResult from the WorkflowHasLastResultReply's
// properties map to be set in the
// WorkflowHasLastResultReply's properties map
func (reply *WorkflowHasLastResultReply) SetHasResult(value bool) {
	reply.SetBoolProperty("HasResult", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowHasLastResultReply) Clone() IProxyMessage {
	workflowHasLastResultReply := NewWorkflowHasLastResultReply()
	var messageClone IProxyMessage = workflowHasLastResultReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowHasLastResultReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowHasLastResultReply); ok {
		v.SetHasResult(reply.GetHasResult())
	}
}
