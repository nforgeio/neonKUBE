package main

import (
	"fmt"
	"io/ioutil"
	"log"
	"os"
	"os/exec"
	"strconv"
	"strings"
	"time"
	
	"github.com/docker/go-plugins-helpers/volume"
)

const socketAddress = "/run/docker/plugins/neon.sock"

type neonDriver struct {

	// Perhaps we'll need some device state
	// here in the future.
}

type errorString struct {

    s string
}

func (e *errorString) Error() string {

    return e.s
}

func toError(text string) error {

    return &errorString{text}
}

func mountPath(volumeName string) string {

	return "/mnt/hivefs/docker/" + volumeName
}

func fsReady() (bool, error) {

	stat, err := os.Stat("/mnt/hivefs/READY")

	if (err == nil && !stat.IsDir()) {

		return true, nil

	} else {

		log.Println("[/mnt/hivefs] is not ready.");
		return false, fsNotReady()
	}
}

func fsNotReady() error {

	return toError("Hive distributed filesystem [/mnt/hivefs] is not ready.")
}

func stub(err error) {
	
	// $hack(jeff.lill)
	//
	// I don't completely understand how GO works and I need
	// a way to workaround the [VARIABLE is declared but not used]
	// compiler errors.  I'm going to handle those by passig them
	// to this "do nothing" function.
}

func fsReadyWait() error {

	// Check if hiveFS is already ready.

	isReady, err := fsReady()
	if (isReady) {
		return nil;
	}

	stub(err)

	// We're going to wait for a period of time for hiveFS
	// to become ready.  This most likely happens during
	// cluster boot where it may take a minute or two for
	// all of the Ceph services to initialize.

	// We'll send [true] on this channel when hiveFS is ready.

	readyChannel := make(chan bool, 1)	
	defer close(readyChannel)

	// This timer will be used to signal a timeout. 

	timer := time.NewTimer(3 * time.Minute)	// 3 minute timeout
	defer timer.Stop()

	// Start a go function that polls for hiveFS to become
	// ready and then signals on the [readyChannel].

	exit := false

	go func() {
		
		for {

			time.Sleep(5 * time.Second)
			
			if (exit) {
				return;
			}

			isReady, err := fsReady()
			if (isReady) {
				readyChannel <- true
				return;
			}

			stub(err)
		}
	}()

	// Wait for a ready signal or a timeout.

	select {
		case <- readyChannel:
		
			log.Println("[/mnt/hivefs] IS READY NOW ***")
			return nil

		case <- timer.C:

			exit = true
			return toError("Timeout waiting for hiveFS to become ready.")
	}
}

func volumeExists(volumeName string) bool {

	path      := mountPath(volumeName)
	stat, err := os.Stat(path)

	if (err == nil && stat.IsDir()) {
		return true
	} else {	
		return false
	}
}

func volumeDoesNotExist(volumeName string) error {

	return toError("volume [" + volumeName + "] does not exist.")
}

func (driver *neonDriver) Create(request *volume.CreateRequest) error {

	log.Println("create:", request.Name);

	error := fsReadyWait()

	if (error != nil) {
		return error
	}
	
	if (volumeExists(request.Name)) {

		// I'm not going to treat this as an error since hiveFS is
		// distributed and its likely that folks may have 
		// already created the volume on another host.

		log.Println("create: volume [" + request.Name + "] already exists.")
		return nil
	}

	// Parse any driver options.  We currently support:
	//
	//		max-bytes	- a simple <long> value or <long>MB <long>GB
	//		max-files	- a simple <long> count

	maxBytes := int64(-1)
	maxFiles := int64(-1)

	if value, ok := request.Options["max-bytes"]; ok {

		log.Println("create: max-bytes =", value);

		value = strings.ToUpper(value)

		if (strings.HasSuffix(value, "MB")) {

			value = value[:len(value) - 2]		// Strip the units

			size, err := strconv.ParseInt(value, 10, 64)

			if (err != nil || size <= 0) {

				log.Println("create: max-bytes is invalid.")
				return toError(fmt.Sprintf("[max-bytes=%v] option is invalid.", value))
			}

			maxBytes = size * 1024 * 1024;
		
		} else if (strings.HasSuffix(value, "GB")) {

			value = value[:len(value) - 2]		// Strip the units

			size, err := strconv.ParseInt(value, 10, 64)

			if (err != nil || size <= 0) {

				log.Println("create: max-bytes is invalid.")
				return toError(fmt.Sprintf("[max-bytes=%v] option is invalid.", value))
			}

			maxBytes = size * 1024 * 1024 * 1024;

		} else {

			size, err := strconv.ParseInt(value, 10, 64)

			if (err != nil || size <= 0) {

				log.Println("create: max-bytes is invalid.")
				return toError(fmt.Sprintf("[max-bytes=%v] option is invalid.", value))
			}

			maxBytes = size
		}
	}

	if value, ok := request.Options["max-files"]; ok {

		log.Println("create: max-files =", value);
	
		count, err := strconv.ParseInt(value, 10, 64)

		if (err != nil || count <= 0) {

			log.Println("create: max-files is invalid.")
			return toError(fmt.Sprintf("[max-files=%v] option is invalid.", value))
		}

		maxFiles = count
	}

	// Create the volume folder.

	mountPath := mountPath(request.Name)

	error = os.MkdirAll(mountPath, 770)
	if (error != nil) {
		log.Println("create error:", error)
	}

	// Set any extended attributes.

	if (maxBytes > 0) {

		maxBytesArg := fmt.Sprintf("%v", maxBytes)

		if err := exec.Command("/usr/bin/setfattr", "-n", "ceph.quota.max_bytes", "-v", maxBytesArg, mountPath).Run(); err != nil {
			return err
		}
	}

	if (maxFiles > 0) {
	
		maxFilesArg := fmt.Sprintf("%v", maxFiles)

		if err := exec.Command("/usr/bin/setfattr", "-n", "ceph.quota.max_files", "-v", maxFilesArg, mountPath).Run(); err != nil {
			return err
		}
	}

	return error;
}

func (driver *neonDriver) Remove(request *volume.RemoveRequest) error {

	log.Println("remove:", request.Name);

	error := fsReadyWait()

	if (error != nil) {
		return error
	}
	
	if (volumeExists(request.Name)) {

		error = os.RemoveAll(mountPath(request.Name))
		if (error != nil) {
			log.Println("remove error:", error)
		}

		return error

	} else {
		return toError("remove: volume [" + request.Name + "] does not exist.")
	}
}

func (driver *neonDriver) Path(request *volume.PathRequest) (*volume.PathResponse, error) {

	log.Println("path:", request.Name);

	error := fsReadyWait()

	if (error != nil) {
		return nil, error
	}

	if (volumeExists(request.Name)) {
		return &volume.PathResponse{Mountpoint: mountPath(request.Name)}, nil
	} else {
		return nil, volumeDoesNotExist(request.Name)
	}
}

func (driver *neonDriver) Mount(request *volume.MountRequest) (*volume.MountResponse, error) {

	log.Println("mount:", request.Name);

	error := fsReadyWait()

	if (error != nil) {
		return nil, error
	}
	
	if (volumeExists(request.Name)) {
		return &volume.MountResponse{Mountpoint: mountPath(request.Name)}, nil
	} else {

		log.Println("path: volume does not exist");
		return nil, volumeDoesNotExist(request.Name)
	}
}

func (driver *neonDriver) Unmount(request *volume.UnmountRequest) error {

	log.Println("unmount:", request.Name);

	error := fsReadyWait()

	if (error != nil) {
		return error
	}
	
	if (volumeExists(request.Name)) {
		return nil
	} else {

		log.Println("unmount: volume does not exist");
		return volumeDoesNotExist(request.Name)
	}
}

func (driver *neonDriver) Get(request *volume.GetRequest) (*volume.GetResponse, error) {

	log.Println("get:", request.Name);

	error := fsReadyWait()

	if (error != nil) {
		return nil, error
	}
	
	if (volumeExists(request.Name)) {
		return &volume.GetResponse{Volume: &volume.Volume{Name: request.Name, Mountpoint: mountPath(request.Name)}}, nil
	} else {

		log.Println("get: volume does not exist");
		return nil, volumeDoesNotExist(request.Name)
	}
}

func (driver *neonDriver) List() (*volume.ListResponse, error) {

	log.Println("list");

	error := fsReadyWait()

	if (error != nil) {
		return nil, error
	}
	
	var volumes []*volume.Volume

	files, err := ioutil.ReadDir("/mnt/hivefs/docker")

	if err == nil {

		for _, file := range files {

			if (file.IsDir()) {
			
				volumes = append(volumes, &volume.Volume{Name: file.Name(), Mountpoint: mountPath(file.Name())})
			}
		}
	}

	return &volume.ListResponse{Volumes: volumes}, nil
}

func (driver *neonDriver) Capabilities() *volume.CapabilitiesResponse {

	log.Println("capabilities");

	return &volume.CapabilitiesResponse{Capabilities: volume.Capability{Scope: "local"}}
}

func main() {

  // Output the plugin version if "version" is passed on the command line.

  args := os.Args[1:]  // Strip off the program path

  if (len(args) > 0 && args[0] == "version") {
      fmt.Println(version)
      return
  }

  // Start the volume plugin service.

	log.Println(fmt.Sprintf("Starting: neon-volume-plugin v%v", version))

	driver  := &neonDriver{ }
	handler := volume.NewHandler(driver)

	handler.ServeUnix(socketAddress, 0)
}
