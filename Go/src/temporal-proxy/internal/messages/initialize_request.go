//-----------------------------------------------------------------------------
// FILE:		initialize_request.go
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
	dotnetlogger "temporal-proxy/internal/dotnet-logger"
)

type (

	// InitializeRequest is ProxyRequest of MessageType
	// InitializeRequest.
	//
	// A InitializeRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	InitializeRequest struct {
		*ProxyRequest
	}
)

// NewInitializeRequest is the default constructor for a InitializeRequest
//
// returns *InitializeRequest -> pointer to a newly initialized
// InitializeRequest in memory
func NewInitializeRequest() *InitializeRequest {
	request := new(InitializeRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.InitializeRequest)
	request.SetReplyType(internal.InitializeReply)

	return request
}

// GetLibraryAddress gets the LibraryAddress property from an InitializeRequest
// in its properties map
//
// returns *string -> a pointer to a string in memory that holds the value
// of an InitializeRequest's LibraryAddress
func (request *InitializeRequest) GetLibraryAddress() *string {
	return request.GetStringProperty("LibraryAddress")
}

// SetLibraryAddress sets the LibraryAddress property in an INitializeRequest's
// properties map
//
// param value *string -> a pointer to a string that holds the LibraryAddress value
// to set in the request's properties map
func (request *InitializeRequest) SetLibraryAddress(value *string) {
	request.SetStringProperty("LibraryAddress", value)
}

// GetLibraryPort gets the LibraryPort property from an InitializeRequest
// in its properties map
//
// returns *string -> a pointer to a string in memory that holds the value
// of an InitializeRequest's LibraryPort
func (request *InitializeRequest) GetLibraryPort() int32 {
	return request.GetIntProperty("LibraryPort")
}

// SetLibraryPort sets the LibraryPort property in an INitializeRequest's
// properties map
//
// param value *string -> a pointer to a string that holds the LibraryPort value
// to set in the request's properties map
func (request *InitializeRequest) SetLibraryPort(value int32) {
	request.SetIntProperty("LibraryPort", value)
}

// GetLogLevel gets the LogLevel property from an InitializeRequest
// in its properties map.  Identifies the log level.
//
// returns dotnetlogger.LogLevel -> InitializeRequest's LogLevel
func (request *InitializeRequest) GetLogLevel() dotnetlogger.LogLevel {
	str := request.GetStringProperty("LogLevel")
	if str == nil {
		return dotnetlogger.None
	}

	return dotnetlogger.ParseLogLevel(*str)
}

// SetLogLevel sets the LogLevel property in an INitializeRequest's
// properties map.  Identifies the log level.
//
// param value dotnetlogger.LogLevel -> InitializeRequest's LogLevel
func (request *InitializeRequest) SetLogLevel(value dotnetlogger.LogLevel) {
	str := value.String()
	request.SetStringProperty("LogLevel", &str)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (request *InitializeRequest) Clone() IProxyMessage {
	initializeRequest := NewInitializeRequest()
	var messageClone IProxyMessage = initializeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (request *InitializeRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*InitializeRequest); ok {
		v.SetLibraryAddress(request.GetLibraryAddress())
		v.SetLibraryPort(request.GetLibraryPort())
		v.SetLogLevel(request.GetLogLevel())
	}
}
