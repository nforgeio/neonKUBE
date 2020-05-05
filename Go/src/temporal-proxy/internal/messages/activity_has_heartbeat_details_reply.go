//-----------------------------------------------------------------------------
// FILE:		activity_has_heartbeat_details_reply.go
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

	// ActivityHasHeartbeatDetailsReply is a ActivityReply of MessageType
	// ActivityHasHeartbeatDetailsReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityHasHeartbeatDetailsRequest
	ActivityHasHeartbeatDetailsReply struct {
		*ActivityReply
	}
)

// NewActivityHasHeartbeatDetailsReply is the default constructor for
// a ActivityHasHeartbeatDetailsReply
//
// returns *ActivityHasHeartbeatDetailsReply -> a pointer to a newly initialized
// ActivityHasHeartbeatDetailsReply in memory
func NewActivityHasHeartbeatDetailsReply() *ActivityHasHeartbeatDetailsReply {
	reply := new(ActivityHasHeartbeatDetailsReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityHasHeartbeatDetailsReply)

	return reply
}

// GetHasDetails gets the HasDetails property from
// a ActivityHasHeartbeatDetailsReply's properties map.
// Indicates whether heartbeat details are available.
//
// returns bool -> bool indicating whether heartbeat details are available.
func (reply *ActivityHasHeartbeatDetailsReply) GetHasDetails() bool {
	return reply.GetBoolProperty("HasDetails")
}

// SetHasDetails sets the HasDetails property in
// a ActivityHasHeartbeatDetailsReply's properties map.
// Indicates whether heartbeat details are available.
//
// param value bool -> bool indicating whether heartbeat details are available,
// to be set in the ActivityHasHeartbeatDetailsReply's properties map
func (reply *ActivityHasHeartbeatDetailsReply) SetHasDetails(value bool) {
	reply.SetBoolProperty("HasDetails", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityHasHeartbeatDetailsReply) Clone() IProxyMessage {
	activityHasHeartbeatDetailsReply := NewActivityHasHeartbeatDetailsReply()
	var messageClone IProxyMessage = activityHasHeartbeatDetailsReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityHasHeartbeatDetailsReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityHasHeartbeatDetailsReply); ok {
		v.SetHasDetails(reply.GetHasDetails())
	}
}
