//-----------------------------------------------------------------------------
// FILE:		activity_start_reply.go
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

	// ActivityStartReply is a WorkflowReply of MessageType
	// ActivityStartReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a ActivityExecuteRequest
	ActivityStartReply struct {
		*WorkflowReply
	}
)

// NewActivityStartReply is the default constructor for
// a ActivityStartReply
//
// returns *ActivityStartReply -> a pointer to a newly initialized
// ActivityStartReply in memory
func NewActivityStartReply() *ActivityStartReply {
	reply := new(ActivityStartReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.ActivityStartReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityStartReply) Clone() IProxyMessage {
	activityStartReply := NewActivityStartReply()
	var messageClone IProxyMessage = activityStartReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityStartReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}
