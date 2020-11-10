//-----------------------------------------------------------------------------
// FILE:		activity_start_reply.go
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
	"temporal-proxy/internal"
)

type (

	// ActivityStartReply is a ActivityReply of MessageType
	// ActivityStartReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityExecuteRequest
	ActivityStartReply struct {
		*ActivityReply
	}
)

// NewActivityStartReply is the default constructor for
// a ActivityStartReply
//
// returns *ActivityStartReply -> a pointer to a newly initialized
// ActivityStartReply in memory
func NewActivityStartReply() *ActivityStartReply {
	reply := new(ActivityStartReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityStartReply)

	return reply
}

// GetReplayStatus gets the ReplayStatus from a ActivityStartReply's properties
// map.
//
// returns internal.ReplayStatus -> the current history replay
// state of an activity
func (reply *ActivityStartReply) GetReplayStatus() internal.ReplayStatus {
	replayStatusPtr := reply.GetStringProperty("ReplayStatus")
	if replayStatusPtr == nil {
		return internal.ReplayStatusUnspecified
	}
	replayStatus := internal.StringToReplayStatus(*replayStatusPtr)

	return replayStatus
}

// SetReplayStatus sets the ReplayStatus in a WorkflowInvokeRequest's properties
// map.
//
// param value internal.ReplayStatus -> the current history replay
// state of an activity
func (reply *ActivityStartReply) SetReplayStatus(value internal.ReplayStatus) {
	status := value.String()
	reply.SetStringProperty("ReplayStatus", &status)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ActivityReply.Build()
func (reply *ActivityStartReply) Build(e error, result ...interface{}) {
	reply.ActivityReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(internal.ReplayStatus); ok {
			reply.SetReplayStatus(v)
		}
	}
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityStartReply) Clone() IProxyMessage {
	activityStartReply := NewActivityStartReply()
	var messageClone IProxyMessage = activityStartReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityStartReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityStartReply); ok {
		v.SetReplayStatus(reply.GetReplayStatus())
	}
}
