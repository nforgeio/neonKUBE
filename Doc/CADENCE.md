# Cadence GitHub Repo and Docker Image Generation

## Introduction

Uber's https://github.com/uber/cadence GitHub repository currently retains only the last few release branches.  This is a problem for our Docker image build scripts because these scripts assume that they can rebuild all image versions at any time.

The solution is to:

* Fork the Uber Cadence to https://github.com/nforgeio/cadence
* Periodically feteching new release branches
* Building Docker images from our repo.

## Tasks

1. **Clone:** You'll need to be clone the https://github.com/nforgeio/cadence repo to your workstation.  This must be called **neon-cadence** and it must be located in the same parent directory that also holds your **neonKUBE** repo (e.g. **C:\src**).

2. **Merge upstream master branch:** Simply run this command to sync our fork's **master** branch with the official Uber repo **master**:

  ```
  neon-cadence sync master
  ```

3. **New release branch:** Cadence names their latest release branch like **0.5.8**.  Note that branches named like **0.5.8_release** are still in progress and have not yet been released.

  You can use the command below to pull a new release branch from the official Cadence repo to your local **neon-cadence** repo and then push it up to https://github.com/nforgeio/cadence:

  ```
  neon-cadence release 0.5.8

  ```
