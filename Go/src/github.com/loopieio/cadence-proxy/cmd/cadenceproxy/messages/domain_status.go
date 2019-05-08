package messages

// DomainStatus is an enumerated list of
// all of the valid cadence domain statuses
type DomainStatus int

const (

	// StatusUnspecified indicates that the status cannot be determined.
	StatusUnspecified DomainStatus = iota

	// Registered indicates that the domain is registered and active.
	Registered

	// Deprecated indicates that the domain is closed for new workflows
	// but will remain until already running workflows are completed and the
	// history retention period for the last executed workflow
	// has been satisified.
	Deprecated
)

func (t DomainStatus) String() string {
	return [...]string{
		"UNSPECIFIED",
		"REGISTERED",
		"DEPRECATED",
	}[t]
}
