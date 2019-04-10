package common

import (
	"bytes"
	"log"

	binary "encoding/binary"
)

type (
	// Operation represents an encoded Cadence operation
	// OpCode is the cadence operation as an enumeration
	// Arguments are the arguments for the operations
	// Attachments are any data attachments (in bytes) that
	// are needed to perform the operation
	Operation struct {
		OpCode      int32
		Arguments   map[string]*string
		Attachments [][]byte
	}
)

const (
	int32ByteSize = 4
	nilValueSize  = -1
)

// OperationToString is a method for cleanly
// printing an operation object to a log console
func (op *Operation) OperationToString() {
	log.Print("{\n")
	log.Println()
	log.Printf("\tOpCode: %d\n", op.OpCode)

	log.Print("\tArguments:\n")
	for k, v := range op.Arguments {
		if v == nil {
			log.Printf("\t\t%s: %s,\n", k, "nil")
		} else {
			log.Printf("\t\t%s: %s,\n", k, *v)
		}
	}

	log.Print("\tAttachments:")
	for i := 0; i < len(op.Attachments); i++ {
		log.Printf("\t\t%v\n", op.Attachments[i])
	}

	log.Print("}\n\n")
}

// PrettyByteSlice takes a []byte and formats it
// array format to be passed as an argument
// in .NET
func PrettyByteSlice(b []byte) {
	fmt.Print("\n{ ")
	for i := 0; i < len(b); i++ {
		fmt.Print(b[i])
		if i == (len(b) - 1) {
			fmt.Print(" }")
		} else {
			fmt.Print(", ")
		}
	}
}

// Int32ToByteSlice takes an int32
// and converts it into a []byte
// Param num int32 -> int32 to be encoded into a []byte
// Returns []byte representation of num param
// Returns error
func Int32ToByteSlice(num int32) ([]byte, error) {

	// create the []byte and make it
	// the size of a 32bit int
	blice := make([]byte, 0, int32ByteSize)

	// create a buffer for to read the bytes into
	buf := bytes.NewBuffer(blice)

	// write to the buffer LittleEndian byte order
	err := binary.Write(buf, binary.LittleEndian, num)
	if err != nil {
		log.Println("binary.Write failed: ", err)
	}

	return buf.Bytes(), err
}

// ByteSliceToInt32 decodes a []byte
// into an int32 and returns the int32
// Param b []byte -> []byte to decode into int32
// Returns int32 value of the []byte parameter
// Returns error
func ByteSliceToInt32(b []byte) (int32, error) {

	var num int32

	// new byte.Reader to read the bytes in b
	buf := bytes.NewReader(b)

	// Read the []byte into the byte.Reader
	// LittleEndian byte order
	err := binary.Read(buf, binary.LittleEndian, &num)
	if err != nil {
		log.Println("binary.Read failed: ", err)
	}

	return num, err
}

// AppendByte takes a []byte and bytes of data
// and safely appends the data to the end of the
// []byte, growing it when necessary
// Param slice []byte -> the []byte you want to append to
// Param data ...byte -> the data you want to append to slice
// Returns the resulting []byte
func AppendByte(slice []byte, data ...byte) []byte {
	m := len(slice)
	n := m + len(data)

	// if necessary, reallocate allocate
	// double what's needed, for future growth.
	if n > cap(slice) {
		newSlice := make([]byte, (n)*2)
		copy(newSlice, slice)
		slice = newSlice
	}

	// slice and copy
	slice = slice[0:n]
	copy(slice[m:n], data)
	return slice
}

// ArgumentsToByteSlice takes in a Operation.Arguments
// as a map[string]*string
// and encodes it into a []byte
// Param m map[string]*string -> Operation.Arguments
// Returns the encoded []byte
func ArgumentsToByteSlice(m map[string]*string) []byte {

	// empty []byte to append to
	b := make([]byte, 0)

	// iterate through the map
	for k, v := range m {

		// first add the size of the key
		keySize := int32(binary.Size([]byte(k)))
		keySizeBytes, err := Int32ToByteSlice(keySize)
		if err != nil {
			log.Println("Could not convert int32 to []byte: ", err)
		}
		for i := 0; i < len(keySizeBytes); i++ {
			b = AppendByte(b, keySizeBytes[i])
		}

		// Next add the key itself
		keyBytes := []byte(k)
		for i := 0; i < len(keyBytes); i++ {
			b = AppendByte(b, keyBytes[i])
		}

		// Next the size of the value
		//
		// catch any nil values and set
		// their length to -1 to indicate nil
		var valueSize int32
		if v == nil {
			valueSize = int32(nilValueSize)
		} else {
			valueSize = int32(binary.Size([]byte(*v)))
		}

		// cast the []byte into an int representing
		// the size of the valueSize []byte
		valueSizeBytes, err := Int32ToByteSlice(valueSize)
		if err != nil {
			log.Println("Could not convert int32 to []byte: ", err)
		}

		// append the value size bytes to the []byte
		for i := 0; i < len(valueSizeBytes); i++ {
			b = AppendByte(b, valueSizeBytes[i])
		}

		// Finally Append the value itself
		//
		// if the value is nil then do not append any bytes
		if valueSize == nilValueSize {
			continue
		} else {
			valueBytes := []byte(*v)
			for i := 0; i < len(valueBytes); i++ {
				b = AppendByte(b, valueBytes[i])
			}
		}
	}

	return b
}

// AttachmentsToByteSlice takes an Operation.Attachment
// as a [][]byte and encodes it into a []byte
// Param bb [][]byte -> Operation.Attachment
// Returns a []byte
func AttachmentsToByteSlice(bb [][]byte) []byte {

	// empty []byte to append to
	b := make([]byte, 0)

	// iterate through the 1st dimension of bb
	for i := 0; i < len(bb); i++ {
		row := bb[i]

		// check for nil Attachments.
		// If there are any then set the size to -1
		var rowSize int32
		if row == nil {
			rowSize = int32(nilValueSize)
		} else {
			rowSize = int32(binary.Size(row))
		}

		// append the size of the attachment
		//
		// If the Attachment is nil set size to -1
		rowSizeBytes, err := Int32ToByteSlice(rowSize)
		if err != nil {
			log.Println("Could not convert int32 to []byte: ", err)
		}

		// append the size bytes
		for j := 0; j < len(rowSizeBytes); j++ {
			b = AppendByte(b, rowSizeBytes[j])
		}

		// apend the attachment itself
		//
		// If the Attachment is nil, do not append anything
		if rowSize == nilValueSize {
			continue
		} else {
			for j := 0; j < len(row); j++ {
				b = AppendByte(b, row[j])
			}
		}
	}

	return b
}

// DecodeArguments takes an Operation.Argument
// encoded into a []byte and decodes it into
// a map[string]*string
// Param b []byte -> Operation.Arguments encoded as a []byte
// Param count int -> number of Operation.Arguments
// Returns a map[string]*string
func DecodeArguments(b []byte, count int) map[string]*string {

	// empty map to fill with decoded
	// key:value pairs
	m := make(map[string]*string)

	// index to slice the []byte on
	sliceIndex := 0

	// the current key in the map
	key := ""

	// iterate through twice because
	// you need to get bytes for both the key
	// and its corresponding value
	for i := 0; i < count*2; i++ {

		// Get the size of the key or value slice by
		// reading the int32 size value encoded in the
		// leading 4 bytes
		slice := b[sliceIndex : sliceIndex+int32ByteSize]
		sliceSize, err := ByteSliceToInt32(slice)
		if err != nil {
			log.Println("Could not convert []byte to int32: ", err)
		}

		// increase the slice index to now
		sliceIndex = sliceIndex + int32ByteSize
		var str string

		// if we are on an even pass,
		// a key is being read,
		// on an odd pass, a value is
		// being read
		if i%2 == 0 {
			// slice at the start of the key or value
			// increase the slice index to the next key:value pair
			str = string(b[sliceIndex : sliceIndex+int(sliceSize)])
			sliceIndex = sliceIndex + int(sliceSize)
			key = str
		} else {
			var strPtr *string

			// catch nil values by checking if sliceSize is -1
			// if so, the map value will be a nil pointer
			if sliceSize == nilValueSize {
				strPtr = nil
				sliceSize = 0
			} else {
				// slice at the start of the key or value
				// increase the slice index to the next key:value pair
				str = string(b[sliceIndex : sliceIndex+int(sliceSize)])
				strPtr = &str
			}

			// set the map key/value pair
			// Iterate the slice index
			sliceIndex = sliceIndex + int(sliceSize)
			m[key] = strPtr

		}
	}

	return m
}

// DecodeAttachments takes Operation.Attachments encoded
// as a []byte and decodes it into an Operation.Attachments
// as a [][]byte
// Param b []byte -> Operation.Attachments encoded as a []byte
// Param count int -> number of Operation.Attachments
//Returns [][]byte
func DecodeAttachments(b []byte, count int) [][]byte {

	// empty [][]byte used to put decoded data into
	bb := make([][]byte, count)

	// index to slice b on
	sliceIndex := 0

	// iterate through number of times there are
	// Operation.Attachments
	for i := 0; i < count; i++ {

		// slice to get the size of the following Operation.Attachments
		slice := b[sliceIndex : sliceIndex+int32ByteSize]
		sliceSize, err := ByteSliceToInt32(slice)
		if err != nil {
			log.Println("Could not convert []byte to int32: ", err)
		}

		// increase the index to slice on the Operation.Attachments itself
		sliceIndex = sliceIndex + int32ByteSize

		// check if slice size is -1
		// i.e. if the row is nil
		if sliceSize == nilValueSize {
			bb[i] = nil
			sliceSize = 0
		} else {
			// slice to get the row and set that in the [][]byte
			row := b[sliceIndex : sliceIndex+int(sliceSize)]
			bb[i] = row
		}

		// increase the index for the next pass
		sliceIndex = sliceIndex + int(sliceSize)
	}

	return bb
}

// OperationToByteSlice takes in an Operation,
// encodes it into a []byte,
// and then returns the encoded []byte
// Param op Operation -> the Operation to be encoded
// Returns []byte -> encoded Operation as a []byte
func OperationToByteSlice(op Operation) []byte {

	// Operation.OpCode bytes
	opCodeBytes, err := Int32ToByteSlice(op.OpCode)
	if err != nil {
		log.Println("Could not convert int32 to []byte: ", err)
	}

	// Operation.Arguments count bytes
	numArguments := int32(len(op.Arguments))
	argumentCountBytes, err := Int32ToByteSlice(numArguments)
	if err != nil {
		log.Println("Could not convert int32 to []byte: ", err)
	}

	// Operation.Arguments bytes
	argumentBytes := ArgumentsToByteSlice(op.Arguments)

	// Operation.Attachments count bytes
	numAttachments := int32(len(op.Attachments))
	attachmentCountBytes, err := Int32ToByteSlice(numAttachments)
	if err != nil {
		log.Println("Could not convert int32 to []byte: ", err)
	}

	// Operation.Attachments bytes
	attachmentBytes := AttachmentsToByteSlice(op.Attachments)

	// Calculate the capacity of the []byte that we need
	size := binary.Size(opCodeBytes) + binary.Size(argumentCountBytes) + binary.Size(argumentBytes) + binary.Size(attachmentCountBytes) + binary.Size(attachmentBytes)

	// empty []byte to append encoded data onto
	b := make([]byte, 0, size)

	// append Operation.OpCode bytes
	for i := 0; i < len(opCodeBytes); i++ {
		b = AppendByte(b, opCodeBytes[i])
	}

	// append Operation.Arguments count bytes
	for i := 0; i < len(argumentCountBytes); i++ {
		b = AppendByte(b, argumentCountBytes[i])
	}

	// append Operation.Arguments bytes
	for i := 0; i < len(argumentBytes); i++ {
		b = AppendByte(b, argumentBytes[i])
	}

	// append Operation.Attachments count bytes
	for i := 0; i < len(attachmentCountBytes); i++ {
		b = AppendByte(b, attachmentCountBytes[i])
	}

	// append Operation.Attachments bytes
	for i := 0; i < len(attachmentBytes); i++ {
		b = AppendByte(b, attachmentBytes[i])
	}

	return b
}

// GetAttachmentsSliceSize calculates the size of
// Operations.Attachments portion of an Operation encoded
// as a []byte
// Param b []byte -> Operation encoded as []byte
// Param count int -> the number of Operation.Attachments
// Param startIndex int -> the index in the []byte where the Attachments begin
// Returns int -> size of the Attachments portion of the encoded Operation
func GetAttachmentsSliceSize(b []byte, count int, startIndex int) int {

	// slice the original []byte to everything
	// after the start index
	buf := b[startIndex:]

	// size accumulator
	size := 0

	// index to slice on
	sliceIndex := 0

	// iterate number of times there are Attachments
	for i := 0; i < count; i++ {

		// slice the size of the Attachment
		slice := buf[sliceIndex : sliceIndex+int32ByteSize]

		// Cast the size into an int
		n, err := ByteSliceToInt32(slice)
		if err != nil {
			log.Println("Could not convert []byte to int32: ", err)
		}

		// check for nil slice size
		// this indicates a nil value for the
		// attachment and no attachment
		// bytes should be counted
		var sliceSize int
		if n == nilValueSize {
			sliceSize = 0
		} else {
			sliceSize = int(n)
		}
		// increment the size and the sliceIndex for the next pass
		size = size + sliceSize + int32ByteSize
		sliceIndex = sliceIndex + sliceSize + int32ByteSize
	}

	return size
}

// GetArgumentsSliceSize calculates the size of
// Operations.Arguments portion of an Operation encoded
// as a []byte
// Param b []byte -> Operation encoded as []byte
// Param count int -> the number of Operation.Arguments
// Param startIndex int -> the index in the []byte where the Arguments begin
// Returns int -> size of the Arguments portion of the encoded Operation
func GetArgumentsSliceSize(b []byte, count int, startIndex int) int {

	// slice the original []byte to everything
	// after the start index
	buf := b[startIndex:]

	// size accumulator
	size := 0

	// index to slice on
	sliceIndex := 0

	// iterate through 2*count because you have to read
	// both keys and values
	for i := 0; i < count*2; i++ {

		// slice the size of the Argument
		slice := buf[sliceIndex : sliceIndex+int32ByteSize]

		// Cast the size into an int
		n, err := ByteSliceToInt32(slice)
		if err != nil {
			log.Println("Could not convert []byte to int32: ", err)
		}

		// check for nil slice size
		// this indicates a nil value for the
		// argument and no argument
		// bytes should be counted
		var sliceSize int
		if n == nilValueSize {
			sliceSize = 0
		} else {
			sliceSize = int(n)
		}

		// increment the size and the sliceIndex for the next pass
		size = size + sliceSize + int32ByteSize
		sliceIndex = sliceIndex + sliceSize + int32ByteSize
	}

	return size
}

// ByteSliceToOperation takes an Operation
// encoded as a []byte and decodes it into an Operation
// Param b []byte -> the encoded Operation as a []byte
// Returns Operation -> the Operation resulting from decoding param b
func ByteSliceToOperation(b []byte) Operation {

	// unilitialized Operation to fill with goods
	var op Operation

	// Get the Operation.OpCode bytes
	index := 0
	opCodeBytes := b[index : index+int32ByteSize]

	// Decode and set the Operation.OpCode
	index = index + int32ByteSize
	opCode, err := ByteSliceToInt32(opCodeBytes)
	if err != nil {
		log.Println("Could not convert []byte to int32: ", err)
	}
	op.OpCode = opCode

	// Get the Operation.Arguments count bytes
	argumentCountBytes := b[index : index+int32ByteSize]
	argumentCount, err := ByteSliceToInt32(argumentCountBytes)
	if err != nil {
		log.Println("Could not convert []byte to int32: ", err)
	}
	intArgumentCount := int(argumentCount)

	// Decode and set the Operation.Arguments
	index = index + int32ByteSize
	argumentSliceSize := GetArgumentsSliceSize(b, intArgumentCount, index)
	argumentBytes := b[index : index+argumentSliceSize]
	op.Arguments = DecodeArguments(argumentBytes, intArgumentCount)

	// Get Operation.Attachments county bytes
	index = index + argumentSliceSize
	attachmentCountBytes := b[index : index+int32ByteSize]
	attachmentCount, err := ByteSliceToInt32(attachmentCountBytes)
	if err != nil {
		log.Println("Could not convert []byte to int32: ", err)
	}
	intAttachmentCount := int(attachmentCount)

	// Decode and set Operation.Attachments
	index = index + int32ByteSize
	attachmentSliceSize := GetAttachmentsSliceSize(b, intAttachmentCount, index)
	attachmentBytes := b[index : index+attachmentSliceSize]
	op.Attachments = DecodeAttachments(attachmentBytes, intAttachmentCount)

	return op
}
