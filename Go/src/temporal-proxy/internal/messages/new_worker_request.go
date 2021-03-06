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
	worker "go.temporal.io/sdk/worker"

	internal "temporal-proxy/internal"
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
	request.SetType(internal.NewWorkerRequest)
	request.SetReplyType(internal.NewWorkerReply)

	return request
}

// GetNamespace gets a NewWorkerRequest's Namespace value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's Namespace
func (request *NewWorkerRequest) GetNamespace() *string {
	return request.GetStringProperty("Namespace")
}

// SetNamespace sets a NewWorkerRequest's Namespace value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetNamespace(value *string) {
	request.SetStringProperty("Namespace", value)
}

// GetTaskQueue gets a NewWorkerRequest's TaskQueue value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's TaskQueue
func (request *NewWorkerRequest) GetTaskQueue() *string {
	return request.GetStringProperty("TaskQueue")
}

// SetTaskQueue sets a NewWorkerRequest's TaskQueue value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetTaskQueue(value *string) {
	request.SetStringProperty("TaskQueue", value)
}

// GetOptions gets a NewWorkerRequest's start options
// used to execute a temporal workflow via the temporal workflow client
//
// returns *worker.WorkerOptions -> pointer to a temporal worker struct that contains the
// options for creating a new worker
func (request *NewWorkerRequest) GetOptions() *worker.Options {
	var opts worker.Options
	err := request.GetJSONProperty("Options", &opts)
	if err != nil {
		return nil
	}

	return &opts
}

// SetOptions sets a NewWorkerRequest's start options
// used to execute a temporal workflow via the temporal workflow client
//
// param value client.StartWorkflowOptions -> pointer to a temporal worker struct
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
		v.SetNamespace(request.GetNamespace())
		v.SetTaskQueue(request.GetTaskQueue())
		v.SetOptions(request.GetOptions())
	}
}
