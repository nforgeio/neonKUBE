//-----------------------------------------------------------------------------
// FILE:		activity_record_heartbeat_reply.go
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

	// ActivityRecordHeartbeatReply is a ActivityReply of MessageType
	// ActivityRecordHeartbeatReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityRecordHeartbeatRequest
	ActivityRecordHeartbeatReply struct {
		*ActivityReply
	}
)

// NewActivityRecordHeartbeatReply is the default constructor for
// a ActivityRecordHeartbeatReply
//
// returns *ActivityRecordHeartbeatReply -> a pointer to a newly initialized
// ActivityRecordHeartbeatReply in memory
func NewActivityRecordHeartbeatReply() *ActivityRecordHeartbeatReply {
	reply := new(ActivityRecordHeartbeatReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityRecordHeartbeatReply)

	return reply
}

// GetDetails gets the Activity heartbeat Details or nil
// from a ActivityRecordHeartbeatReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity heartbeat Details
func (reply *ActivityRecordHeartbeatReply) GetDetails() []byte {
	return reply.GetBytesProperty("Details")
}

// SetDetails sets the Activity heartbeat Details or nil
// in a ActivityRecordHeartbeatReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity heartbeat
// Details, to be set in the ActivityRecordHeartbeatReply's properties map
func (reply *ActivityRecordHeartbeatReply) SetDetails(value []byte) {
	reply.SetBytesProperty("Details", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityRecordHeartbeatReply) Clone() IProxyMessage {
	activityRecordHeartbeatReply := NewActivityRecordHeartbeatReply()
	var messageClone IProxyMessage = activityRecordHeartbeatReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityRecordHeartbeatReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityRecordHeartbeatReply); ok {
		v.SetDetails(reply.GetDetails())
	}
}
