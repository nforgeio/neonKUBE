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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
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
		GetContextID() int64
		SetContextID(value int64)
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
	reply.SetType(messagetypes.Unspecified)

	return reply
}

// -------------------------------------------------------------------------
// IActivityReply interface methods for implementing the IActivityReply interface

// GetContextID gets the ContextId from a ActivityReply's properties
// map.
//
// returns int64 -> the long representing a ActivityReply's ContextId
func (reply *ActivityReply) GetContextID() int64 {
	return reply.GetLongProperty("ContextId")
}

// SetContextID sets the ContextId in a ActivityReply's properties map
//
// param value int64 -> int64 value to set as the ActivityReply's ContextId
// in its properties map
func (reply *ActivityReply) SetContextID(value int64) {
	reply.SetLongProperty("ContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *ActivityReply) Clone() IProxyMessage {
	workflowContextReply := NewActivityReply()
	var messageClone IProxyMessage = workflowContextReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *ActivityReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(IActivityReply); ok {
		v.SetContextID(reply.GetContextID())
	}
}
