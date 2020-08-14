//-----------------------------------------------------------------------------
// FILE:		activity_reply.go
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

	// ActivityReply is base type for all workflow replies.
	// All workflow replies will inherit from ActivityReply
	//
	// A ActivityReply contains a reference to a
	// ProxyReply struct in memory
	ActivityReply struct {
		*ProxyReply
	}

	// IActivityReply is the interface that all workflow message replies
	// implement.
	IActivityReply interface {
		IProxyReply
		GetActivityContextID() int64
		SetActivityContextID(value int64)
	}
)

// NewActivityReply is the default constructor for ActivityReply.
// It creates a new ActivityReply in memory and then creates and sets
// a reference to a new ProxyReply in the ActivityReply.
//
// returns *ActivityReply -> a pointer to a new ActivityReply in memory
func NewActivityReply() *ActivityReply {
	reply := new(ActivityReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.Unspecified)

	return reply
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetActivityContextID gets the ActivityContextId from a ActivityReply's properties
// map.
//
// returns int64 -> the long representing a ActivityReply's ActivityContextId
func (reply *ActivityReply) GetActivityContextID() int64 {
	return reply.GetLongProperty("ActivityContextId")
}

// SetActivityContextID sets the ActivityContextId in a ActivityReply's properties map
//
// param value int64 -> int64 value to set as the ActivityReply's ActivityContextId
// in its properties map
func (reply *ActivityReply) SetActivityContextID(value int64) {
	reply.SetLongProperty("ActivityContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *ActivityReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *ActivityReply) Clone() IProxyMessage {
	activityContextReply := NewActivityReply()
	var messageClone IProxyMessage = activityContextReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *ActivityReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(IActivityReply); ok {
		v.SetActivityContextID(reply.GetActivityContextID())
	}
}
