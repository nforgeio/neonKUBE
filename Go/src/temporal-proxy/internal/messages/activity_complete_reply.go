//-----------------------------------------------------------------------------
// FILE:		activity_complete_reply.go
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

	// ActivityCompleteReply is a ActivityReply of MessageType
	// ActivityCompleteReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityCompleteRequest
	ActivityCompleteReply struct {
		*ActivityReply
	}
)

// NewActivityCompleteReply is the default constructor for
// a ActivityCompleteReply
//
// returns *ActivityCompleteReply -> a pointer to a newly initialized
// ActivityCompleteReply in memory
func NewActivityCompleteReply() *ActivityCompleteReply {
	reply := new(ActivityCompleteReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityCompleteReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ActivityReply.Build()
func (reply *ActivityCompleteReply) Build(e error, result ...interface{}) {
	reply.ActivityReply.Build(e)
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityCompleteReply) Clone() IProxyMessage {
	activityCompleteReply := NewActivityCompleteReply()
	var messageClone IProxyMessage = activityCompleteReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityCompleteReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
}
