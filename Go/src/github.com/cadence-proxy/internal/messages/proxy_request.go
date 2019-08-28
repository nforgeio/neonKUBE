//-----------------------------------------------------------------------------
// FILE:		proxy_request.go
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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// ProxyRequest "extends" ProxyMessage and it is
	// a type of ProxyMessage that comes into the server
	// i.e. a request
	//
	// A ProxyRequest contains a RequestId and a reference to a
	// ProxyMessage struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	ProxyRequest struct {
		*ProxyMessage
		ReplyType messagetypes.MessageType
	}

	// IProxyRequest is an interface for all ProxyRequest message types.
	// It allows any message type that implements the IProxyRequest interface
	// to use any methods defined.  The primary use of this interface is to
	// allow message types that implement it to get and set their nested ProxyRequest
	IProxyRequest interface {
		IProxyMessage
		GetReplyType() messagetypes.MessageType
		SetReplyType(value messagetypes.MessageType)
		GetIsCancellable() bool
		SetIsCancellable(value bool)
	}
)

// NewProxyRequest is the default constructor for a ProxyRequest
//
// returns *ProxyRequest -> a pointer to a newly initialized ProxyRequest
// in memory
func NewProxyRequest() *ProxyRequest {
	request := new(ProxyRequest)
	request.ProxyMessage = NewProxyMessage()
	request.SetType(messagetypes.Unspecified)
	request.SetReplyType(messagetypes.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType gets the MessageType used to reply to a specific
// ProxyRequest
//
// returns MessageType -> the message type to reply to the
// request with
func (request *ProxyRequest) GetReplyType() messagetypes.MessageType {
	return request.ReplyType
}

// SetReplyType sets the MessageType used to reply to a specific
// ProxyRequest
//
// param value MessageType -> the message type to reply to the
// request with
func (request *ProxyRequest) SetReplyType(value messagetypes.MessageType) {
	request.ReplyType = value
}

// GetIsCancellable gets the IsCancellable property from the
// properties map of a ProxyRequest.  This indicates whether the
// operation should be cancellable.
//
// returns bool -> boolean IsCancellable property.
func (request *ProxyRequest) GetIsCancellable() bool {
	return request.GetBoolProperty("IsCancellable")
}

// SetIsCancellable sets the IsCancellable property in the
// properties map of a ProxyRequest.  This indicates whether the
// operation should be cancellable.
//
// param bool value -> boolean IsCancellable property to set in
// the ProxyRequest's properties map.
func (request *ProxyRequest) SetIsCancellable(value bool) {
	request.SetBoolProperty("IsCancellable", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *ProxyRequest) Clone() IProxyMessage {
	proxyRequest := NewProxyRequest()
	var messageClone IProxyMessage = proxyRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *ProxyRequest) CopyTo(target IProxyMessage) {
	request.ProxyMessage.CopyTo(target)
	if v, ok := target.(IProxyRequest); ok {
		v.SetReplyType(request.GetReplyType())
		v.SetIsCancellable(request.GetIsCancellable())
	}
}
