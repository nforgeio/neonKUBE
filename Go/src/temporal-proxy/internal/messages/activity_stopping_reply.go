//-----------------------------------------------------------------------------
// FILE:		activity_stopping_reply.go
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
	proxyerror "temporal-proxy/internal/temporal/error"
)

type (

	// ActivityStoppingReply is a ActivityReply of MessageType
	// ActivityStoppingReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityStoppingRequest
	ActivityStoppingReply struct {
		*ActivityReply
	}
)

// NewActivityStoppingReply is the default constructor for
// a ActivityStoppingReply
//
// returns *ActivityStoppingReply -> a pointer to a newly initialized
// ActivityStoppingReply in memory
func NewActivityStoppingReply() *ActivityStoppingReply {
	reply := new(ActivityStoppingReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityStoppingReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ActivityReply.Build()
func (reply *ActivityStoppingReply) Build(e *proxyerror.TemporalError, result ...interface{}) {
	reply.ActivityReply.Build(e)
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityStoppingReply) Clone() IProxyMessage {
	activityStoppingReply := NewActivityStoppingReply()
	var messageClone IProxyMessage = activityStoppingReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityStoppingReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
}
