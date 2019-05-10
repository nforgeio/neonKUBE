package cadenceerrors

import "fmt"

// CadenceErrorTypes is an enumerated list of
// all of the cadence error types
type CadenceErrorTypes int

const (

	// Cancelled indicates that an operation was cancelled
	Cancelled CadenceErrorTypes = iota

	// Custom is a custom error
	Custom

	// Generic is a generic error
	Generic

	// Panic is a panic error
	Panic

	// Terminated is a termination error
	Terminated

	// Timeout is a timeout error
	Timeout
)

func (t CadenceErrorTypes) String() string {
	return [...]string{
		"cancelled",
		"custom",
		"generic",
		"panic",
		"terminated",
		"timeout",
	}[t]
}

// ErrorTypeToString takes a CadenceErrorTypes and converts it into the corresponding
// string representation
//
// param errorType CadenceErrorType -> the CadenceErrorTypes to convert to a string
//
// returns string -> the string representation of the param errorType CadenceErrorTypes
func ErrorTypeToString(errorType CadenceErrorTypes) string {
	var typeString string
	switch errorType {
	case Cancelled:
		typeString = "cancelled"
	case Custom:
		typeString = "custom"
	case Generic:
		typeString = "generic"
	case Panic:
		typeString = "panic"
	case Terminated:
		typeString = "terminated"
	case Timeout:
		typeString = "timeout"
	default:
		err := fmt.Errorf("unrecognized error type %s", errorType)
		panic(err)
	}

	return typeString
}
