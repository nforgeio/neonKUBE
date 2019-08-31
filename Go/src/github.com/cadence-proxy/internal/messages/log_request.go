//-----------------------------------------------------------------------------
// FILE:		log_request.go
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

	"github.com/cadence-proxy/internal/messages/dotnet-logger"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// LogRequest is ProxyRequest of MessageType
	// LogRequest.
	//
	// A LogRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	LogRequest struct {
		*ProxyRequest
	}
)

// NewLogRequest is the default constructor for a LogRequest
//
// returns *LogRequest -> pointer to a newly initialized
// LogRequest in memory
func NewLogRequest() *LogRequest {
	request := new(LogRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.LogRequest)
	request.SetReplyType(messagetypes.LogReply)

	return request
}

// GetTimeUtc gets the Time property from the LogRequest's
// properties map. Identifies when the event being
// logged occurred (UTC).
//
// returns time.Time -> the value of the Time property from
// the LogRequest's properties map.
func (request *LogRequest) GetTimeUtc() time.Time {
	return request.GetDateTimeProperty("Time")
}

// SetTimeUtc sets the Time property in the LogRequest's
// properties map. Identifies when the event being
// logged occurred (UTC).
//
// param value time.Time -> the Time to be set in the
// LogRequest's properties map.
func (request *LogRequest) SetTimeUtc(value time.Time) {
	request.SetDateTimeProperty("Time", value)
}

// GetLogLevel gets the LogLevel property from an LogRequest
// in its properties map.  Identifies the log level.
//
// returns dotnetlogger.LogLevel -> LogRequest's LogLevel
func (request *LogRequest) GetLogLevel() dotnetlogger.LogLevel {
	str := request.GetStringProperty("LogLevel")
	if str == nil {
		return dotnetlogger.None
	}

	return dotnetlogger.ParseLogLevel(*str)
}

// SetLogLevel sets the LogLevel property in an INitializeRequest's
// properties map.  Identifies the log level.
//
// param value dotnetlogger.LogLevel -> LogRequest's LogLevel
func (request *LogRequest) SetLogLevel(value dotnetlogger.LogLevel) {
	str := value.String()
	request.SetStringProperty("LogLevel", &str)
}

// GetFromCadence gets a LogRequest's FromCadence value from
// its nested properties map. Specifies the source of the event veing logged.
// Set this to true for events coming from the GOLANG Cadence client or false
// for events coming from the cadence-proxy wrapper.
//
// returns bool -> bool indicating if the message to be logged is from
// native Cadence.
func (request *LogRequest) GetFromCadence() bool {
	return request.GetBoolProperty("FromCadence")
}

// SetFromCadence sets a LogionRequest's FromCadence in
// its nested properties map. Specifies the source of the event veing logged.
// Set this to true for events coming from the GOLANG Cadence client or false
// for events coming from the cadence-proxy wrapper.
//
// param value bool -> bool indicating if the message to be logged is from
// native Cadence.
func (request *LogRequest) SetFromCadence(value bool) {
	request.SetBoolProperty("FromCadence", value)
}

// GetLogMessage gets the LogMessage property from an LogRequest
// in its properties map.  The message being logged.
//
// returns *string -> a pointer to a string in memory that holds the value
// of an LogRequest's LogMessage
func (request *LogRequest) GetLogMessage() *string {
	return request.GetStringProperty("LogMessage")
}

// SetLogMessage sets the LogMessage property in an INitializeRequest's
// properties map.  The message being logged.
//
// param value *string -> a pointer to a string that holds the LogMessage value
// to set in the request's properties map
func (request *LogRequest) SetLogMessage(value *string) {
	request.SetStringProperty("LogMessage", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (request *LogRequest) Clone() IProxyMessage {
	logRequest := NewLogRequest()
	var messageClone IProxyMessage = logRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (request *LogRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*LogRequest); ok {
		v.SetTimeUtc(request.GetTimeUtc())
		v.SetLogLevel(request.GetLogLevel())
		v.SetFromCadence(request.GetFromCadence())
		v.SetLogMessage(request.GetLogMessage())
	}
}
