package domain

import "fmt"

// DomainStatus is an enumerated list of
// all of the valid cadence domain statuses
type DomainStatus int

const (

	// Registered indicates that the domain is registered and active.
	Registered DomainStatus = 0

	// Deprecated indicates that the domain is closed for new workflows
	// but will remain until already running workflows are completed and the
	// history retention period for the last executed workflow
	// has been satisified.
	Deprecated DomainStatus = 1

	// Deleted indicates that a cadence domain has been deleted
	Deleted DomainStatus = 2
)

func (t DomainStatus) String() string {
	return [...]string{
		"REGISTERED",
		"DEPRECATED",
		"DELETED",
	}[t]
}

func StringToDomainStatus(value string) DomainStatus {
	switch value {
	case "REGISTERED":
		return Registered
	case "DEPRECATED":
		return Deprecated
	case "DELETED":
		return Deleted
	default:
		err := fmt.Errorf("unknown string value %s for %s", value, "DomainStatus")
		panic(err)
	}
}
