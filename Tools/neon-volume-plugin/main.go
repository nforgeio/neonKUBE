package main

import (
	"fmt"
	"io/ioutil"
	"os"
	
	"github.com/docker/go-plugins-helpers/volume"
)

const socketAddress = "/run/docker/plugins/neon-volume.sock"

type neonDriver struct {

	// Perhaps we'll have some properties in the future.
}

func mountPath(volumeName string) string {
	return "/cfs/docker/" + volumeName
}

func (driver *neonDriver) Create(request *volume.CreateRequest) error {
	
	fmt.Println("create:", request.Name);

	error := os.MkdirAll(mountPath(request.Name), 770)
	if (error != nil) {
		fmt.Println("create error:", error)
	}

	return error;
}

func (driver *neonDriver) Remove(request *volume.RemoveRequest) error {

	fmt.Println("remove:", request.Name);

	error := os.RemoveAll(mountPath(request.Name))
	if (error != nil) {
		fmt.Println("create error:", error)
	}

	return error;
}

func (driver *neonDriver) Path(request *volume.PathRequest) (*volume.PathResponse, error) {

	fmt.Println("path:", request.Name);

	return &volume.PathResponse{Mountpoint: mountPath(request.Name)}, nil
}

func (driver *neonDriver) Mount(request *volume.MountRequest) (*volume.MountResponse, error) {

	fmt.Println("mount:", request.Name);

	return &volume.MountResponse{Mountpoint: mountPath(request.Name)}, nil
}

func (driver *neonDriver) Unmount(request *volume.UnmountRequest) error {

	fmt.Println("unmount:", request.Name);

	return nil
}

func (driver *neonDriver) Get(request *volume.GetRequest) (*volume.GetResponse, error) {

	fmt.Println("get:", request.Name);

	return &volume.GetResponse{Volume: &volume.Volume{Name: request.Name, Mountpoint: mountPath(request.Name)}}, nil
}

func (driver *neonDriver) List() (*volume.ListResponse, error) {

	fmt.Println("list");

	var volumes []*volume.Volume

	files, err := ioutil.ReadDir("/cfs/docker")

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

	fmt.Println("capabilities");

	return &volume.CapabilitiesResponse{Capabilities: volume.Capability{Scope: "local"}}
}

func main() {

	fmt.Println("Starting Docker [neon-volume] driver plugin")

	driver  := &neonDriver{ }
	handler := volume.NewHandler(driver)

	handler.ServeUnix(socketAddress, 0)
}
