//-----------------------------------------------------------------------------
// FILE:		activity_start_local_reply.go
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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// ActivityStartLocalReply is a ActivityReply of MessageType
	// ActivityStartReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityExecuteRequest
	ActivityStartLocalReply struct {
		*ActivityReply
	}
)

// NewActivityStartLocalReply is the default constructor for
// a ActivityStartLocalReply
//
// returns *ActivityStartLocalReply -> a pointer to a newly initialized
// ActivityStartLocalReply in memory
func NewActivityStartLocalReply() *ActivityStartLocalReply {
	reply := new(ActivityStartLocalReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityStartLocalReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityStartLocalReply) Clone() IProxyMessage {
	activityStartLocalReply := NewActivityStartLocalReply()
	var messageClone IProxyMessage = activityStartLocalReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityStartLocalReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
}
