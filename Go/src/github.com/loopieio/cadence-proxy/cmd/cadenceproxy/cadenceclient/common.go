package cadenceclient

import (
	"fmt"
	"reflect"
	"sync"

	"go.uber.org/cadence/client"
)

var (
	mu sync.RWMutex

	// CadenceClientConnections is a map that will hold all
	// currently open cadence client connections to the cadence server for
	// the running cadence-proxy instance
	CadenceClientConnections map[string]client.Client = make(map[string]client.Client)

	// DomainClientConnections is a map that will hold all
	// currently open domain client connections to the cadence server for
	// the running cadence-proxy instance
	DomainClientConnections map[string]client.DomainClient = make(map[string]client.DomainClient)

	// ClientHelper is a global variable that holds this cadence-proxy's instance
	// of the CadenceClientHelper that will be used to create domain and workflow clients
	// that communicate with the cadence server
	ClientHelper *CadenceClientHelper
)

// Add is a thread safe way to add a value to both the CadenceClientConnections
// and DomainClientConnections maps.  Guarded by a mutex lock
//
// param v map[string]interface{} -> the desired map to add an entry to
// param key string -> the key to add the entry at
// param value interface{} -> the interface value to add at the specified entry
// in the map v
//
// returns error -> error if the value was not able to be added to the map
// this can happen if the value is of an incorrect type to add it to the map
func Add(v map[string]interface{}, key string, value interface{}) error {
	if reflect.TypeOf(value) != reflect.TypeOf(CadenceClientConnections) ||
		reflect.TypeOf(value) != reflect.TypeOf(DomainClientConnections) {
		return fmt.Errorf("incorrect interface type %v for this map", reflect.TypeOf(value))
	}

	mu.Lock()
	v[key] = value
	mu.Unlock()

	return nil
}

// Remove is a thread safe method for removing a single element from a
// map.
//
// param v map[string]interface{} -> the desired map to add an entry to
// param key string -> the key to add the entry at
//
// returns interface{} -> the value of the element that was just removed
func Remove(v map[string]interface{}, key string) interface{} {
	remove := GetEntry(v, key)
	mu.Lock()
	v[key] = nil
	mu.Unlock()

	return remove
}

// GetEntry is a thread safe method for reading an entry from a map with
// a string key.
//
// param v map[string]interface{} -> the desired map to add an entry to
// param key string -> the key to add the entry at
//
// returns interface{} -> the value of the element at the specified key
func GetEntry(v map[string]interface{}, key string) interface{} {
	var remove interface{}
	mu.RLock()
	remove = v[key]
	mu.RUnlock()

	return remove
}
