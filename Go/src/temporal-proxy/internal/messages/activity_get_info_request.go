//-----------------------------------------------------------------------------
// FILE:		activity_get_info_request.go
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

	// ActivityGetInfoRequest is an ActivityRequest of MessageType
	// ActivityGetInfoRequest.
	//
	// A ActivityGetInfoRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Requests the details for the last heartbeat
	// recorded for a failed previous run of the activity.
	ActivityGetInfoRequest struct {
		*ActivityRequest
	}
)

// NewActivityGetInfoRequest is the default constructor for a ActivityGetInfoRequest
//
// returns *ActivityGetInfoRequest -> a pointer to a newly initialized ActivityGetInfoRequest
// in memory
func NewActivityGetInfoRequest() *ActivityGetInfoRequest {
	request := new(ActivityGetInfoRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(internal.ActivityGetInfoRequest)
	request.SetReplyType(internal.ActivityGetInfoReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityGetInfoRequest) Clone() IProxyMessage {
	activityGetInfoRequest := NewActivityGetInfoRequest()
	var messageClone IProxyMessage = activityGetInfoRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityGetInfoRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
}
