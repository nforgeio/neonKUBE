//-----------------------------------------------------------------------------
// FILE:		activity_execute_local_reply.go
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

	// ActivityExecuteLocalReply is a ActivityReply of MessageType
	// ActivityExecuteLocalReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityExecuteLocalRequest
	ActivityExecuteLocalReply struct {
		*ActivityReply
	}
)

// NewActivityExecuteLocalReply is the default constructor for
// a ActivityExecuteLocalReply
//
// returns *ActivityExecuteLocalReply -> a pointer to a newly initialized
// ActivityExecuteLocalReply in memory
func NewActivityExecuteLocalReply() *ActivityExecuteLocalReply {
	reply := new(ActivityExecuteLocalReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityExecuteLocalReply)

	return reply
}

// GetResult gets the activity results encoded as a byte array result or nil
// from a ActivityExecuteLocalReply's properties map.
//
// returns []byte -> []byte representing the result of a Activity execution
func (reply *ActivityExecuteLocalReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the activity results encoded as a byte array result or nil
// in a ActivityExecuteLocalReply's properties map.
//
// param value []byte -> []byte representing the result of a Activity execution
// to be set in the ActivityExecuteLocalReply's properties map
func (reply *ActivityExecuteLocalReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityReply.Clone()
func (reply *ActivityExecuteLocalReply) Clone() IProxyMessage {
	activityExecuteLocalReply := NewActivityExecuteLocalReply()
	var messageClone IProxyMessage = activityExecuteLocalReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityReply.CopyTo()
func (reply *ActivityExecuteLocalReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityExecuteLocalReply); ok {
		v.SetResult(reply.GetResult())
	}
}
