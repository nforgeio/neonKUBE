//-----------------------------------------------------------------------------
// FILE:		activity_get_heartbeat_details_reply.go
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

	// ActivityGetHeartbeatDetailsReply is a ActivityReply of MessageType
	// ActivityGetHeartbeatDetailsReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityGetHeartbeatDetailsRequest
	ActivityGetHeartbeatDetailsReply struct {
		*ActivityReply
	}
)

// NewActivityGetHeartbeatDetailsReply is the default constructor for
// a ActivityGetHeartbeatDetailsReply
//
// returns *ActivityGetHeartbeatDetailsReply -> a pointer to a newly initialized
// ActivityGetHeartbeatDetailsReply in memory
func NewActivityGetHeartbeatDetailsReply() *ActivityGetHeartbeatDetailsReply {
	reply := new(ActivityGetHeartbeatDetailsReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityGetHeartbeatDetailsReply)

	return reply
}

// GetDetails gets the Activity heartbeat Details or nil
// from a ActivityGetHeartbeatDetailsReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity heartbeat Details
func (reply *ActivityGetHeartbeatDetailsReply) GetDetails() []byte {
	return reply.GetBytesProperty("Details")
}

// SetDetails sets the Activity heartbeat Details or nil
// in a ActivityGetHeartbeatDetailsReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity heartbeat
// Details, to be set in the ActivityGetHeartbeatDetailsReply's properties map
func (reply *ActivityGetHeartbeatDetailsReply) SetDetails(value []byte) {
	reply.SetBytesProperty("Details", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityGetHeartbeatDetailsReply) Clone() IProxyMessage {
	activityGetHeartbeatDetailsReply := NewActivityGetHeartbeatDetailsReply()
	var messageClone IProxyMessage = activityGetHeartbeatDetailsReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityGetHeartbeatDetailsReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityGetHeartbeatDetailsReply); ok {
		v.SetDetails(reply.GetDetails())
	}
}
