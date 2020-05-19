//-----------------------------------------------------------------------------
// FILE:		workflow_get_version_reply.go
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

	// WorkflowGetVersionReply is a WorkflowReply of MessageType
	// WorkflowGetVersionReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowGetVersionRequest
	WorkflowGetVersionReply struct {
		*WorkflowReply
	}
)

// NewWorkflowGetVersionReply is the default constructor for
// a WorkflowGetVersionReply
//
// returns *WorkflowGetVersionReply -> a pointer to a newly initialized
// WorkflowGetVersionReply in memory
func NewWorkflowGetVersionReply() *WorkflowGetVersionReply {
	reply := new(WorkflowGetVersionReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowGetVersionReply)

	return reply
}

// GetVersion gets the Version property from a WorkflowGetVersionReply's
// properties map. Returns the workflow implementation version.
//
// returns int32 -> value of the Version property
func (reply *WorkflowGetVersionReply) GetVersion() int32 {
	return reply.GetIntProperty("Version")
}

// SetVersion sets the Version property in a WorkflowGetVersionReply's
// properties map. Returns the workflow implementation version.
//
// param value int32 -> value of the Version property to be set in the
// WorkflowGetVersionReply's properties map
func (reply *WorkflowGetVersionReply) SetVersion(value int32) {
	reply.SetIntProperty("Version", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowGetVersionReply) Clone() IProxyMessage {
	workflowGetVersionReply := NewWorkflowGetVersionReply()
	var messageClone IProxyMessage = workflowGetVersionReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowGetVersionReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowGetVersionReply); ok {
		v.SetVersion(reply.GetVersion())
	}
}
