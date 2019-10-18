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
)

type (

	// QueueMap thread-safe map of
	// chan []byte to their QueueID's
	QueueMap struct {
		sync.Mutex                       // protects read and writes to the map
		queues     map[int64]chan []byte // map of QueueID to chan []byte
	}
)

//----------------------------------------------------------------------------
// QueueMap instance methods

// NewQueueMap is the constructor for a QueueMap
func NewQueueMap() *QueueMap {
	q := new(QueueMap)
	q.queues = make(map[int64]chan []byte)
	return q
}

// Add adds a new []byte chan to the QueueMap map at the specified
// queueID. This method is thread-safe.
//
// param queueID int64 -> the long queueID. This will be the mapped key.
//
// param b chan []byte -> A chan []byte. This will be the mapped value
//
// returns int64 -> long queueID of the newly added queue.
func (q *QueueMap) Add(queueID int64, b chan []byte) int64 {
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
// returns chan []byte -> a chan []byte representing the queue.
func (q *QueueMap) Get(queueID int64) chan []byte {
	q.Lock()
	defer q.Unlock()
	return q.queues[queueID]
}
