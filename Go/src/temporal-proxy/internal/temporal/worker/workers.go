//-----------------------------------------------------------------------------
// FILE:		workers.go
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

package worker

import (
	"sync"

	"go.temporal.io/temporal/worker"
)

var (
	mu sync.RWMutex

	// workerID is incremented (protected by a mutex) every time
	// a new temporal Worker is created
	workerID int64
)

type (

	// WorkersMap holds a thread-safe map[interface{}]interface{} that stores
	// temporal Workers with their workerID's
	WorkersMap struct {
		sync.Mutex
		workers map[int64]worker.Worker
	}
)

//----------------------------------------------------------------------------
// workerID methods

// NextWorkerID increments the global variable
// workerID by 1 and is protected by a mutex lock
func NextWorkerID() int64 {
	mu.Lock()
	workerID = workerID + 1
	defer mu.Unlock()
	return workerID
}

// GetWorkerID gets the value of the global variable
// workerID and is protected by a mutex Read lock
func GetWorkerID() int64 {
	mu.RLock()
	defer mu.RUnlock()
	return workerID
}

//----------------------------------------------------------------------------
// WorkersMap instance methods

// NewWorkersMap is the constructor for an WorkersMap
func NewWorkersMap() *WorkersMap {
	o := new(WorkersMap)
	o.workers = make(map[int64]worker.Worker)
	return o
}

// Add adds a new temporal worker and its corresponding WorkerId into
// the Workers.workers map.  This method is thread-safe.
//
// param workerID int64 -> the long workerID to the temporal Worker
// returned by the Temporal NewWorker() function.  This will be the mapped key
//
// param worker worker.Worker -> pnew temporal Worker returned
// by the Temporal NewWorker() function.  This will be the mapped value
//
// returns int64 -> long workerID of the new temporal Worker added to the map
func (w *WorkersMap) Add(workerID int64, worker worker.Worker) int64 {
	w.Lock()
	defer w.Unlock()
	w.workers[workerID] = worker
	return workerID
}

// Remove removes key/value entry from the Workers map at the specified
// WorkerId.  This is a thread-safe method.
//
// param workerID int64 -> the long workerID to the temporal Worker
// returned by the Temporal NewWorker() function.  This will be the mapped key
//
// returns int64 -> long workerID of the temporal Worker removed from the map
func (w *WorkersMap) Remove(workerID int64) int64 {
	w.Lock()
	defer w.Unlock()
	delete(w.workers, workerID)
	return workerID
}

// Get gets a temporal Worker from the WorkersMap at the specified
// workerID.  This method is thread-safe.
//
// param workerID int64 -> the long workerID to the temporal Worker
// returned by the Temporal NewWorker() function.  This will be the mapped key
//
// returns worker.Worker -> temporal Worker with the specified workerID
func (w *WorkersMap) Get(workerID int64) worker.Worker {
	w.Lock()
	defer w.Unlock()
	return w.workers[workerID]
}
