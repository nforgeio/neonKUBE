//-----------------------------------------------------------------------------
// FILE:		activities.go
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

	// ActivityMap holds a thread-safe map of
	// temporal Activity to the ActivityID..
	ActivityMap struct {
		sync.Mutex
		activites map[int64]Activity
	}

	// Activity represents a temporal activity workflow execution.
	// It holds a workflow Future and cancellation function
	Activity struct {
		future     workflow.Future     // temporal Future
		cancelFunc workflow.CancelFunc // temporal workflow CancelFunc for canceling the child
	}
)

//----------------------------------------------------------------------------
// Activity instance methods

// NewActivity is the default constructor
// for a Activity struct
//
// param future workflow.Future -> the Future associated
// with an instance of a activity.
//
// param cancel workflow.CancelFunc -> the activity's cancellation function.
//
// returns *Activity -> pointer to a newly initialized
// Activity in memory
func NewActivity(future workflow.Future, cancel workflow.CancelFunc) *Activity {
	a := new(Activity)
	a.SetFuture(future)
	a.SetCancelFunction(cancel)
	return a
}

// GetCancelFunction gets a Activity's context cancel function
//
// returns workflow.CancelFunc -> a temporal workflow context cancel function
func (a *Activity) GetCancelFunction() workflow.CancelFunc {
	return a.cancelFunc
}

// SetCancelFunction sets a Activity's cancel function
//
// param value workflow.CancelFunc -> a temporal workflow context cancel function
func (a *Activity) SetCancelFunction(value workflow.CancelFunc) {
	a.cancelFunc = value
}

// GetFuture gets a Activity's workflow.Future
//
// returns workflow.Future -> a temporal workflow.Future
func (a *Activity) GetFuture() workflow.Future {
	return a.future
}

// SetFuture sets a Activity's workflow.Future
//
// param value workflow.Future -> a temporal workflow.Future to be
// set as a Activity's temporal workflow.Future
func (a *Activity) SetFuture(value workflow.Future) {
	a.future = value
}

//----------------------------------------------------------------------------
// ActivitiesMap instance methods

// NewActivityMap is the constructor for an ActivitiesMap
func NewActivityMap() *ActivityMap {
	o := new(ActivityMap)
	o.activites = make(map[int64]Activity)
	return o
}

// Add adds a new temporal context and its corresponding ContextId into
// the ActivitiesMap map.  This method is thread-safe.
//
// param activityID int64 -> the long activityID of activity.
// This will be the mapped key.
//
// param activity Activity -> a workflow activity.
//
// returns int64 -> long activityID of the new temporal workflow.Future added to the map
func (a *ActivityMap) Add(activityID int64, activity Activity) int64 {
	a.Lock()
	defer a.Unlock()
	a.activites[activityID] = activity
	return activityID
}

// Remove removes key/value entry from the ActivitiesMap map at the specified
// ContextId.  This is a thread-safe method.
//
// param activityID int64 -> the long activityID of activity.
// This will be the mapped key.
//
// returns int64 -> long activityID of the Activity removed from the map
func (a *ActivityMap) Remove(activityID int64) int64 {
	a.Lock()
	defer a.Unlock()
	delete(a.activites, activityID)
	return activityID
}

// Get gets a Activity from the ActivitiesMap at the specified
// ActivityID.  This method is thread-safe.
//
// param activityID int64 -> the long activityID of activity.
// This will be the mapped key.
//
// returns Activity -> the activity execution at the specified Id.
func (a *ActivityMap) Get(activityID int64) Activity {
	a.Lock()
	defer a.Unlock()
	return a.activites[activityID]
}
