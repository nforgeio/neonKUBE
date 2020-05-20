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

	internal "temporal-proxy/internal"
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
	request.SetType(internal.ConnectRequest)
	request.SetReplyType(internal.ConnectReply)

	return request
}

// GetHostPort gets a ConnectRequest's HostPort value from
// its nested properties map
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's HostPort
func (request *ConnectRequest) GetHostPort() *string {
	return request.GetStringProperty("HostPort")
}

// SetHostPort sets a ConnectionRequest's HostPort in
// its nested properties map
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetHostPort(value *string) {
	request.SetStringProperty("HostPort", value)
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
// ClientTimeout is a timespan property and indicates the timeout for a temporal client request
//
// returns time.Duration -> the duration for a ConnectRequest's timeout from its properties map
func (request *ConnectRequest) GetClientTimeout() time.Duration {
	return request.GetTimeSpanProperty("ClientTimeout")
}

// SetClientTimeout sets the ClientTimeout property in a ConnectRequest's properties map
// ClientTimeout is a timespan property and indicates the timeout for a temporal client request
//
// param value time.Duration -> the timeout duration to be set in a
// ConnectRequest's properties map
func (request *ConnectRequest) SetClientTimeout(value time.Duration) {
	request.SetTimeSpanProperty("ClientTimeout", value)
}

// GetNamespace gets a ConnectRequest's namespace value from
// its nested properties map. The default Temporal namespace.
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's namespace
func (request *ConnectRequest) GetNamespace() *string {
	return request.GetStringProperty("Namespace")
}

// SetNamespace sets a ConnectionRequest's namespace in
// its nested properties map. The default Temporal namespace.
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetNamespace(value *string) {
	request.SetStringProperty("Namespace", value)
}

// GetCreateNamespace gets a ConnectRequest's CreateNamespace value from
// its nested properties map. Indicates whether the Temporal
// namespace should be created if it doesn't already exist.
//
// returns bool -> bool indicating that the default namespace should
// be created if it does not already exist
func (request *ConnectRequest) GetCreateNamespace() bool {
	return request.GetBoolProperty("CreateNamespace")
}

// SetCreateNamespace sets a ConnectionRequest's CreateNamespace in
// its nested properties map. Indicates whether the Temporal
// namespace should be created if it doesn't already exist.
//
// param value bool -> bool indicating that the default namespace should
// be created if it does not already exist
func (request *ConnectRequest) SetCreateNamespace(value bool) {
	request.SetBoolProperty("CreateNamespace", value)
}

// GetRetries gets a ConnectRequest's RetryAttempts value from
// its nested properties map. Specifies the number of time
// the client will attempt to connect to the Temporal cluster.
//
// returns int32 -> int32 number of retries to connect
func (request *ConnectRequest) GetRetries() int32 {
	return request.GetIntProperty("RetryAttempts")
}

// SetRetries sets a ConnectionRequest's RetryAttempts in
// its nested properties map. Specifies the number of time
// the client will attempt to connect to the Temporal cluster.
//
// param value int32 -> int32 number of retries to connect
func (request *ConnectRequest) SetRetries(value int32) {
	request.SetIntProperty("RetryAttempts", value)
}

// GetRetryDelay gets the RetryDelay property from a ConnectRequest's properties map.
// RetryDelay is a timespan property that specifies the time to
// delay before retrying to connect to the cluster.
//
// returns time.Duration -> the retry delay for a ConnectRequest
func (request *ConnectRequest) GetRetryDelay() time.Duration {
	return request.GetTimeSpanProperty("RetryDelay")
}

// SetRetryDelay sets the RetryDelay property in a ConnectRequest's properties map.
// RetryDelay is a timespan property that specifies the time to
// delay before retrying to connect to the cluster.
//
// param value time.Duration -> the retry delay for a ConnectRequest
func (request *ConnectRequest) SetRetryDelay(value time.Duration) {
	request.SetTimeSpanProperty("RetryDelay", value)
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
		v.SetHostPort(request.GetHostPort())
		v.SetIdentity(request.GetIdentity())
		v.SetClientTimeout(request.GetClientTimeout())
		v.SetNamespace(request.GetNamespace())
		v.SetCreateNamespace(request.GetCreateNamespace())
		v.SetRetries(request.GetRetries())
		v.SetRetryDelay(request.GetRetryDelay())
	}
}
