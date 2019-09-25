//-----------------------------------------------------------------------------
// FILE:		futures.go
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

package activity

import (
	"sync"

	"go.uber.org/cadence/workflow"
)

type (

	// ActivityFuturesMap holds a thread-safe map of
	// cadence workflow.Future to the ActivityID of the cadence
	// workflow activity the future belongs to.
	ActivityFuturesMap struct {
		sync.Mutex
		futures map[int64]workflow.Future
	}
)

//----------------------------------------------------------------------------
// ActivityFuturesMap instance methods

// NewActivityFuturesMap is the constructor for an ActivityFuturesMap
func NewActivityFuturesMap() *ActivityFuturesMap {
	o := new(ActivityFuturesMap)
	o.futures = make(map[int64]workflow.Future)
	return o
}

// Add adds a new cadence context and its corresponding ContextId into
// the ActivityFuturesMap map.  This method is thread-safe.
//
// param activityID int64 -> the long activityID of activity.
// This will be the mapped key.
//
// param future workflow.Future -> workflow future result of activity execution.
//
// returns int64 -> long activityID of the new cadence workflow.Future added to the map
func (a *ActivityFuturesMap) Add(activityID int64, future workflow.Future) int64 {
	a.Lock()
	defer a.Unlock()
	a.futures[activityID] = future
	return activityID
}

// Remove removes key/value entry from the ActivityFuturesMap map at the specified
// ContextId.  This is a thread-safe method.
//
// param activityID int64 -> the long activityID of activity.
// This will be the mapped key.
//
// returns int64 -> long activityID of the workflow.Future removed from the map
func (a *ActivityFuturesMap) Remove(activityID int64) int64 {
	a.Lock()
	defer a.Unlock()
	delete(a.futures, activityID)
	return activityID
}

// Get gets a workflow.Future from the ActivityFuturesMap at the specified
// ActivityID.  This method is thread-safe.
//
// param activityID int64 -> the long activityID of activity.
// This will be the mapped key.
//
// returns workflow.Future -> workflow future result of activity execution at the specified Id.
func (a *ActivityFuturesMap) Get(activityID int64) workflow.Future {
	a.Lock()
	defer a.Unlock()
	return a.futures[activityID]
}
