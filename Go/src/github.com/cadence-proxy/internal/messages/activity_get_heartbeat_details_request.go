//-----------------------------------------------------------------------------
// FILE:		activity_get_heartbeat_details_request.go
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

	// ActivityGetHeartbeatDetailsRequest is an ActivityRequest of MessageType
	// ActivityGetHeartbeatDetailsRequest.
	//
	// A ActivityGetHeartbeatDetailsRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Requests the details for the last heartbeat
	// recorded for a failed previous run of the activity.
	ActivityGetHeartbeatDetailsRequest struct {
		*ActivityRequest
	}
)

// NewActivityGetHeartbeatDetailsRequest is the default constructor for a ActivityGetHeartbeatDetailsRequest
//
// returns *ActivityGetHeartbeatDetailsRequest -> a pointer to a newly initialized ActivityGetHeartbeatDetailsRequest
// in memory
func NewActivityGetHeartbeatDetailsRequest() *ActivityGetHeartbeatDetailsRequest {
	request := new(ActivityGetHeartbeatDetailsRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(internal.ActivityGetHeartbeatDetailsRequest)
	request.SetReplyType(internal.ActivityGetHeartbeatDetailsReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityGetHeartbeatDetailsRequest) Clone() IProxyMessage {
	activityGetHeartbeatDetailsRequest := NewActivityGetHeartbeatDetailsRequest()
	var messageClone IProxyMessage = activityGetHeartbeatDetailsRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityGetHeartbeatDetailsRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
}
