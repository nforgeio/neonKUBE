//-----------------------------------------------------------------------------
// FILE:		proxy_reply.go
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

	// ProxyReply is a IProxyMessage type used for replying to
	// a proxy message request.  It implements the IProxyMessage interface
	// and holds a reference to a ProxyMessage
	ProxyReply struct {
		*ProxyMessage
	}

	// IProxyReply is an interface for all ProxyReply message types.
	// It allows any message type that implements the IProxyReply interface
	// to use any methods defined.  The primary use of this interface is to
	// allow message types that implement it to get and set their nested ProxyReply
	IProxyReply interface {
		IProxyMessage
		GetError() *proxyerror.TemporalError
		SetError(value *proxyerror.TemporalError)
	}
)

// NewProxyReply is the default constructor for ProxyReply.
// It creates a new ProxyReply in memory and then creates and sets
// a reference to a new ProxyMessage in the ProxyReply.
//
// returns *ProxyReply -> a pointer to a new ProxyReply in memory
func NewProxyReply() *ProxyReply {
	reply := new(ProxyReply)
	reply.ProxyMessage = NewProxyMessage()
	reply.SetType(internal.Unspecified)

	return reply
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError gets the TemporalError encoded as a JSON string in a ProxyReply's
// Properties map
//
// returns proxyerror.TemporalError -> a TemporalError struct encoded with the
// JSON property values at a ProxyReply's Error property
func (reply *ProxyReply) GetError() *proxyerror.TemporalError {
	temporalError := proxyerror.NewTemporalErrorEmpty()
	err := reply.GetJSONProperty("Error", temporalError)
	if err != nil {
		return nil
	}

	return temporalError
}

// SetError sets a TemporalError as a JSON string in a ProxyReply's
// properties map at the Error Property
//
// param proxyerror.TemporalError -> the TemporalError to marshal into a
// JSON string and set at a ProxyReply's Error property
func (reply *ProxyReply) SetError(value *proxyerror.TemporalError) {
	reply.SetJSONProperty("Error", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ProxyReply) Clone() IProxyMessage {
	proxyReply := NewProxyReply()
	var messageClone IProxyMessage = proxyReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ProxyReply) CopyTo(target IProxyMessage) {
	reply.ProxyMessage.CopyTo(target)
	if v, ok := target.(IProxyReply); ok {
		v.SetError(reply.GetError())
	}
}
