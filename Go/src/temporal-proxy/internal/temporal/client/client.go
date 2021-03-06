//-----------------------------------------------------------------------------
// FILE:		clients.go
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

package client

import (
	"sync"
)

type (

	// ClientsMap holds a thread-safe map[int64]*Helper that stores
	// temporal workflow service clients with their clientID's
	ClientsMap struct {
		sync.Mutex
		clients map[int64]*Helper
	}
)

//----------------------------------------------------------------------------
// ClientsMap instance methods

// NewClientsMap is the constructor for an ClientsMap
func NewClientsMap() *ClientsMap {
	o := new(ClientsMap)
	o.clients = make(map[int64]*Helper)
	return o
}

// Add adds a new Helper and its corresponding
// ClientId into the Clients map.  This method is thread-safe.
//
// param clientID int64 -> the long clientID to the Helper
// to be added to the ClientsMap.  This will be the mapped key
//
// param client *Helper -> *Client Helper to be added to the
// ClientsMap. This will be the mapped value.
//
// returns int64 -> long clientID of the new Helper
// added to the map.
func (c *ClientsMap) Add(clientID int64, helper *Helper) int64 {
	c.Lock()
	defer c.Unlock()
	c.clients[clientID] = helper
	return clientID
}

// Remove removes key/value entry from the Clients map at the specified
// ClientId.  This is a thread-safe method.
//
// param clientID int64 -> the long clientID to the Helper
// to be removed from the map. This will be the mapped key
//
// returns int64 -> long clientID of the Helper
// to be removed from the map
func (c *ClientsMap) Remove(clientID int64) int64 {
	c.Lock()
	defer c.Unlock()
	delete(c.clients, clientID)
	return clientID
}

// Get gets a Helper from the ClientsMap at
// the specified clientID.  This method is thread-safe.
//
// param clientID int64 -> the long clientID to the Helper
// to be gotten from the ClientsMap.  This will be the mapped key
//
// returns *Helper -> Helper at the specified clientID
func (c *ClientsMap) Get(clientID int64) *Helper {
	c.Lock()
	defer c.Unlock()
	return c.clients[clientID]
}
