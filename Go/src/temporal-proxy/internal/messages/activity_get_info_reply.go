//-----------------------------------------------------------------------------
// FILE:		activity_get_info_reply.go
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
	"go.temporal.io/temporal/activity"
)

type (

	// ActivityGetInfoReply is a ActivityReply of MessageType
	// ActivityGetInfoReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityGetInfoRequest
	ActivityGetInfoReply struct {
		*ActivityReply
	}
)

// NewActivityGetInfoReply is the default constructor for
// a ActivityGetInfoReply
//
// returns *ActivityGetInfoReply -> a pointer to a newly initialized
// ActivityGetInfoReply in memory
func NewActivityGetInfoReply() *ActivityGetInfoReply {
	reply := new(ActivityGetInfoReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityGetInfoReply)

	return reply
}

// GetInfo gets the Activity info or nil
// from a ActivityGetInfoReply's properties map.
// Returns the activity information.
//
// returns *activity.Info -> *activity.Info containing the info of a Activity
func (reply *ActivityGetInfoReply) GetInfo() *activity.Info {
	info := new(activity.Info)
	err := reply.GetJSONProperty("Info", info)
	if err != nil {
		return nil
	}

	return info
}

// SetInfo sets the Activity info or nil
// in a ActivityGetInfoReply's properties map.
// Returns the activity information.
//
// param value *activity.Info -> *activity.Info containing the info of a Activity
// to be set in the ActivityGetInfoReply's properties map
func (reply *ActivityGetInfoReply) SetInfo(value *activity.Info) {
	reply.SetJSONProperty("Info", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityGetInfoReply) Clone() IProxyMessage {
	activityGetInfoReply := NewActivityGetInfoReply()
	var messageClone IProxyMessage = activityGetInfoReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityGetInfoReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityGetInfoReply); ok {
		v.SetInfo(reply.GetInfo())
	}
}
