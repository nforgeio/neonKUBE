//-----------------------------------------------------------------------------
// FILE:		activity_execute_reply.go
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

	// ActivityExecuteReply is a ActivityReply of MessageType
	// ActivityExecuteReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityExecuteRequest
	ActivityExecuteReply struct {
		*ActivityReply
	}
)

// NewActivityExecuteReply is the default constructor for
// a ActivityExecuteReply
//
// returns *ActivityExecuteReply -> a pointer to a newly initialized
// ActivityExecuteReply in memory
func NewActivityExecuteReply() *ActivityExecuteReply {
	reply := new(ActivityExecuteReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityExecuteReply)

	return reply
}

// GetResult gets the Activity execution result or nil
// from a ActivityExecuteReply's properties map.
//
// returns []byte -> []byte representing the result of a Activity execution
func (reply *ActivityExecuteReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the Activity execution result or nil
// in a ActivityExecuteReply's properties map.
//
// param value []byte -> []byte representing the result of a Activity execution
// to be set in the ActivityExecuteReply's properties map
func (reply *ActivityExecuteReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityExecuteReply) Clone() IProxyMessage {
	activityExecuteReply := NewActivityExecuteReply()
	var messageClone IProxyMessage = activityExecuteReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityExecuteReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityExecuteReply); ok {
		v.SetResult(reply.GetResult())
	}
}
