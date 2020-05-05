//-----------------------------------------------------------------------------
// FILE:		activity_invoke_reply.go
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

	// ActivityInvokeReply is a ActivityReply of MessageType
	// ActivityInvokeReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityInvokeRequest
	ActivityInvokeReply struct {
		*ActivityReply
	}
)

// NewActivityInvokeReply is the default constructor for
// a ActivityInvokeReply
//
// returns *ActivityInvokeReply -> a pointer to a newly initialized
// ActivityInvokeReply in memory
func NewActivityInvokeReply() *ActivityInvokeReply {
	reply := new(ActivityInvokeReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityInvokeReply)

	return reply
}

// GetResult gets the Activity execution result or nil
// from a ActivityInvokeReply's properties map.
// Returns the activity results encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity results
func (reply *ActivityInvokeReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the Activity execution result or nil
// in a ActivityInvokeReply's properties map.
// Returns the activity results encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity
// results, to be set in the ActivityInvokeReply's properties map
func (reply *ActivityInvokeReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// GetPending gets the ActivityInvokeReply's Pending property
// from its properties map.
// Indicates that the activity will be completed externally.
//
// returns bool -> bool indiciation if the activity will
// be completed externally
func (reply *ActivityInvokeReply) GetPending() bool {
	return reply.GetBoolProperty("Pending")
}

// SetPending sets the the ActivityInvokeReply's Pending property
// in its properties map.
// Indicates that the activity will be completed externally.
//
// param value bool -> bool indiciation if the activity will
// be completed externally
func (reply *ActivityInvokeReply) SetPending(value bool) {
	reply.SetBoolProperty("Pending", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityInvokeReply) Clone() IProxyMessage {
	activityInvokeReply := NewActivityInvokeReply()
	var messageClone IProxyMessage = activityInvokeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityInvokeReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityInvokeReply); ok {
		v.SetResult(reply.GetResult())
		v.SetPending(reply.GetPending())
	}
}
