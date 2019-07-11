//-----------------------------------------------------------------------------
// FILE:		new_worker_request.go
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
	worker "go.uber.org/cadence/worker"

	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// NewWorkerRequest is ProxyRequest of MessageType
	// NewWorkerRequest.
	//
	// A NewWorkerRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	NewWorkerRequest struct {
		*ProxyRequest
	}
)

// NewNewWorkerRequest is the default constructor for a NewWorkerRequest
//
// returns *NewWorkerRequest -> a reference to a newly initialized
// NewWorkerRequest in memory
func NewNewWorkerRequest() *NewWorkerRequest {
	request := new(NewWorkerRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.NewWorkerRequest)
	request.SetReplyType(messagetypes.NewWorkerReply)

	return request
}

// GetDomain gets a NewWorkerRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's Domain
func (request *NewWorkerRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a NewWorkerRequest's Domain value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetTaskList gets a NewWorkerRequest's TaskList value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's TaskList
func (request *NewWorkerRequest) GetTaskList() *string {
	return request.GetStringProperty("TaskList")
}

// SetTaskList sets a NewWorkerRequest's TaskList value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetTaskList(value *string) {
	request.SetStringProperty("TaskList", value)
}

// GetOptions gets a NewWorkerRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// returns *worker.WorkerOptions -> pointer to a cadence worker struct that contains the
// options for creating a new worker
func (request *NewWorkerRequest) GetOptions() *worker.Options {
	opts := new(worker.Options)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a NewWorkerRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// param value client.StartWorkflowOptions -> pointer to a cadence worker struct
// that contains the options for creating a new worker to be set in the NewWorkerRequest's
// properties map
func (request *NewWorkerRequest) SetOptions(value *worker.Options) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *NewWorkerRequest) Clone() IProxyMessage {
	newWorkerRequest := NewNewWorkerRequest()
	var messageClone IProxyMessage = newWorkerRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *NewWorkerRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*NewWorkerRequest); ok {
		v.SetDomain(request.GetDomain())
		v.SetTaskList(request.GetTaskList())
		v.SetOptions(request.GetOptions())
	}
}
