//-----------------------------------------------------------------------------
// FILE:		activity_invoke_local_reply.go
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

	// ActivityInvokeLocalReply is a ActivityReply of MessageType
	// ActivityInvokeLocalReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityInvokeLocalRequest
	ActivityInvokeLocalReply struct {
		*ActivityReply
	}
)

// NewActivityInvokeLocalReply is the default constructor for
// a ActivityInvokeLocalReply
//
// returns *ActivityInvokeLocalReply -> a pointer to a newly initialized
// ActivityInvokeLocalReply in memory
func NewActivityInvokeLocalReply() *ActivityInvokeLocalReply {
	reply := new(ActivityInvokeLocalReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityInvokeLocalReply)

	return reply
}

// GetResult gets the activity results encoded as a byte array result or nil
// from a ActivityInvokeLocalReply's properties map.
//
// returns []byte -> []byte representing the result of a Activity execution
func (reply *ActivityInvokeLocalReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the activity results encoded as a byte array result or nil
// in a ActivityInvokeLocalReply's properties map.
//
// param value []byte -> []byte representing the result of a Activity execution
// to be set in the ActivityInvokeLocalReply's properties map
func (reply *ActivityInvokeLocalReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityReply.Clone()
func (reply *ActivityInvokeLocalReply) Clone() IProxyMessage {
	activityInvokeLocalReply := NewActivityInvokeLocalReply()
	var messageClone IProxyMessage = activityInvokeLocalReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityReply.CopyTo()
func (reply *ActivityInvokeLocalReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityInvokeLocalReply); ok {
		v.SetResult(reply.GetResult())
	}
}
