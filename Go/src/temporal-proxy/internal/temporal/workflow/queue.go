//-----------------------------------------------------------------------------
// FILE:		queue.go
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

package workflow

import (
	"sync"

	"go.temporal.io/sdk/workflow"
)

type (

	// QueueMap thread-safe map of
	// workflow.Channel to their QueueID's
	QueueMap struct {
		sync.Mutex                            // protects read and writes to the map
		queues     map[int64]workflow.Channel // map of QueueID to workflow.Channel
	}
)

//----------------------------------------------------------------------------
// QueueMap instance methods

// NewQueueMap is the constructor for a QueueMap
func NewQueueMap() *QueueMap {
	q := new(QueueMap)
	q.queues = make(map[int64]workflow.Channel)
	return q
}

// Add adds a new []byte chan to the QueueMap map at the specified
// queueID. This method is thread-safe.
//
// param queueID int64 -> the long queueID. This will be the mapped key.
//
// param b workflow.Channel -> A workflow.Channel. This will be the mapped value
//
// returns int64 -> long queueID of the newly added queue.
func (q *QueueMap) Add(queueID int64, b workflow.Channel) int64 {
	q.Lock()
	defer q.Unlock()
	q.queues[queueID] = b
	return queueID
}

// Remove removes key/value entry from the QueueMap map at the specified
// queueID.  This is a thread-safe method.
//
// param queueID int64 -> the long queueID. This will be the mapped key.
//
// returns int64 -> long queueID of the removed queue.
func (q *QueueMap) Remove(queueID int64) int64 {
	q.Lock()
	defer q.Unlock()
	delete(q.queues, queueID)
	return queueID
}

// Get gets a queue from the QueueMap at the specified
// queueID.  This method is thread-safe.
//
// param queueID int64 -> the long queueID. This will be the mapped key.
//
// returns workflow.Channel -> a workflow.Channel representing the queue.
func (q *QueueMap) Get(queueID int64) workflow.Channel {
	q.Lock()
	defer q.Unlock()
	return q.queues[queueID]
}
