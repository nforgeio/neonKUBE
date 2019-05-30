package cadenceerrors

import "fmt"

// CadenceErrorType is an enumerated list of
// all of the cadence error types
type CadenceErrorType int

const (

	// Cancelled indicates that an operation was cancelled
	Cancelled CadenceErrorType = 0

	// Custom is a custom error
	Custom CadenceErrorType = 1

	// Generic is a generic error
	Generic CadenceErrorType = 2

	// Panic is a panic error
	Panic CadenceErrorType = 3

	// Terminated is a termination error
	Terminated CadenceErrorType = 4

	// Timeout is a timeout error
	Timeout CadenceErrorType = 5
)

func (t CadenceErrorType) String() string {
	return [...]string{
		"cancelled",
		"custom",
		"generic",
		"panic",
		"terminated",
		"timeout",
	}[t]
}

// ToCadenceError takes a string value and converts it into the corresponding
// CadenceErrorType
//
// param value string -> the string representation of a CadenceErrorType
//
// returns CadenceErrorType -> the corresponding CadenceErrorType
func ToCadenceError(value string) CadenceErrorType {
	var errType CadenceErrorType
	switch value {
	case "cancelled":
		errType = Cancelled
	case "custom":
		errType = Custom
	case "generic":
		errType = Generic
	case "panic":
		errType = Panic
	case "terminated":
		errType = Terminated
	case "timeout":
		errType = Timeout
	default:
		err := fmt.Errorf("unrecognized error type %s", value)
		panic(err)
	}

	return errType
}
