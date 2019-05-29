package cadenceworkers

import (
	"sync"

	"go.uber.org/cadence/worker"
)

var (
	mu sync.RWMutex

	// workerID is incremented (protected by a mutex) every time
	// a new cadence Worker is created
	workerID int64

	// Workers maps a int64 WorkerId to the cadence
	// Worker returned by the Cadence NewWorker() function.
	// This will be used to stop a worker via the
	// StopWorkerRequest.
	Workers = new(WorkersMap)
)

type (

	// WorkersMap holds a thread-safe map[interface{}]interface{} that stores
	// cadence Workers with their workerID's
	WorkersMap struct {
		sync.Map
	}
)

//----------------------------------------------------------------------------
// workerID methods

// NextWorkerID increments the global variable
// workerID by 1 and is protected by a mutex lock
func NextWorkerID() int64 {
	mu.Lock()
	curr := workerID
	workerID = workerID + 1
	mu.Unlock()

	return curr
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

// Add adds a new cadence worker and its corresponding WorkerId into
// the Workers.workers map.  This method is thread-safe.
//
// param workerID int64 -> the long workerID to the cadence Worker
// returned by the Cadence NewWorker() function.  This will be the mapped key
//
// param worker worker.Worker -> pnew cadence Worker returned
// by the Cadence NewWorker() function.  This will be the mapped value
//
// returns int64 -> long workerID of the new cadence Worker added to the map
func (workers *WorkersMap) Add(workerID int64, worker worker.Worker) int64 {
	workers.Store(workerID, worker)
	return workerID
}

// Remove removes key/value entry from the Workers map at the specified
// WorkerId.  This is a thread-safe method.
//
// param workerID int64 -> the long workerID to the cadence Worker
// returned by the Cadence NewWorker() function.  This will be the mapped key
//
// returns int64 -> long workerID of the cadence Worker removed from the map
func (workers *WorkersMap) Remove(workerID int64) int64 {
	workers.Delete(workerID)
	return workerID
}

// Get gets a cadence Worker from the WorkersMap at the specified
// workerID.  This method is thread-safe.
//
// param workerID int64 -> the long workerID to the cadence Worker
// returned by the Cadence NewWorker() function.  This will be the mapped key
//
// returns worker.Worker -> cadence Worker with the specified workerID
func (workers *WorkersMap) Get(workerID int64) worker.Worker {
	if v, ok := workers.Load(workerID); ok {
		if _v, _ok := v.(worker.Worker); _ok {
			return _v
		}
	}

	return nil
}
