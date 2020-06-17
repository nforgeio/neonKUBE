//-----------------------------------------------------------------------------
// FILE:		activity_register_reply.go
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

	// ActivityRegisterReply is a ActivityReply of MessageType
	// ActivityRegisterReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityRegisterRequest
	ActivityRegisterReply struct {
		*ActivityReply
	}
)

// NewActivityRegisterReply is the default constructor for
// a ActivityRegisterReply
//
// returns *ActivityRegisterReply -> a pointer to a newly initialized
// ActivityRegisterReply in memory
func NewActivityRegisterReply() *ActivityRegisterReply {
	reply := new(ActivityRegisterReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(internal.ActivityRegisterReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ActivityReply.Build()
func (reply *ActivityRegisterReply) Build(e *proxyerror.TemporalError, result ...interface{}) {
	reply.ActivityReply.Build(e)
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityRegisterReply) Clone() IProxyMessage {
	activityRegisterReply := NewActivityRegisterReply()
	var messageClone IProxyMessage = activityRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityRegisterReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
}
