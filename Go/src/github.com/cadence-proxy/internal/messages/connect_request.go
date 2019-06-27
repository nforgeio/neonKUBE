//-----------------------------------------------------------------------------
// FILE:		connect_request.go
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
	"time"

	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// ConnectRequest is ConnectRequest of MessageType
	// ConnectRequest.
	//
	// A ConnectRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	ConnectRequest struct {
		*ProxyRequest
	}
)

// NewConnectRequest is the default constructor for a ConnectRequest
//
// returns *ConnectRequest -> a reference to a newly initialized
// ConnectRequest in memory
func NewConnectRequest() *ConnectRequest {
	request := new(ConnectRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.ConnectRequest)
	request.SetReplyType(messagetypes.ConnectReply)

	return request
}

// GetEndpoints gets a ConnectRequest's endpoints value from
// its nested properties map
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's endpoints
func (request *ConnectRequest) GetEndpoints() *string {
	return request.GetStringProperty("Endpoints")
}

// SetEndpoints sets a ConnectionRequest's endpoints in
// its nested properties map
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetEndpoints(value *string) {
	request.SetStringProperty("Endpoints", value)
}

// GetIdentity gets a ConnectRequest's identity value from
// its nested properties map
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's identity
func (request *ConnectRequest) GetIdentity() *string {
	return request.GetStringProperty("Identity")
}

// SetIdentity sets a ConnectionRequest's identity in
// its nested properties map
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetIdentity(value *string) {
	request.SetStringProperty("Identity", value)
}

// GetClientTimeout gets the ClientTimeout property from a ConnectRequest's properties map
// ClientTimeout is a timespan property and indicates the timeout for a cadence client request
//
// returns time.Duration -> the duration for a ConnectRequest's timeout from its properties map
func (request *ConnectRequest) GetClientTimeout() time.Duration {
	return request.GetTimeSpanProperty("ClientTimeout", time.Second*30)
}

// SetClientTimeout sets the ClientTimeout property in a ConnectRequest's properties map
// ClientTimeout is a timespan property and indicates the timeout for a cadence client request
//
// param value time.Duration -> the timeout duration to be set in a
// ConnectRequest's properties map
func (request *ConnectRequest) SetClientTimeout(value time.Duration) {
	request.SetTimeSpanProperty("ClientTimeout", value)
}

// GetDomain gets a ConnectRequest's domain value from
// its nested properties map. The default Cadence domain.
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's domain
func (request *ConnectRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a ConnectionRequest's domain in
// its nested properties map. The default Cadence domain.
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetCreateDomain gets a ConnectRequest's CreateDomain value from
// its nested properties map. Indicates whether the Cadence
// domain should be created if it doesn't already exist.
//
// returns bool -> bool indicating that the default domain should
// be created if it does not already exist
func (request *ConnectRequest) GetCreateDomain() bool {
	return request.GetBoolProperty("CreateDomain")
}

// SetCreateDomain sets a ConnectionRequest's CreateDomain in
// its nested properties map. Indicates whether the Cadence
// domain should be created if it doesn't already exist.
//
// param value bool -> bool indicating that the default domain should
// be created if it does not already exist
func (request *ConnectRequest) SetCreateDomain(value bool) {
	request.SetBoolProperty("CreateDomain", value)
}

// -------------------------------------------------------------------------
// ProxyMessage interface methods for implementing the ProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *ConnectRequest) Clone() IProxyMessage {
	connectRequest := NewConnectRequest()
	var messageClone IProxyMessage = connectRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *ConnectRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*ConnectRequest); ok {
		v.SetEndpoints(request.GetEndpoints())
		v.SetIdentity(request.GetIdentity())
		v.SetClientTimeout(request.GetClientTimeout())
		v.SetDomain(request.GetDomain())
		v.SetCreateDomain(request.GetCreateDomain())
	}
}
