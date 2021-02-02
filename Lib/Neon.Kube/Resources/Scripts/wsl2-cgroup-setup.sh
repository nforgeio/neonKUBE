#!/bin/bash
# This script is executed for WSL2 hosting environments when installing Kubernetes.
# We borrowed this from the opensource Kubernetes-IN-Docker (KIND) project.
#
#   https://d2iq.com/blog/running-kind-inside-a-kubernetes-cluster-for-continuous-integration

# helper used by fix_cgroup
mount_kubelet_cgroup_root() {
  local cgroup_root=$1
  local subsystem=$2
  if [ -z "${cgroup_root}" ]; then
    return 0
  fi
  mkdir -p "${subsystem}/${cgroup_root}"
  if [ "${subsystem}" == "/sys/fs/cgroup/cpuset" ]; then
    # This is needed. Otherwise, assigning process to the cgroup
    # (or any nested cgroup) would result in ENOSPC.
    cat "${subsystem}/cpuset.cpus" > "${subsystem}/${cgroup_root}/cpuset.cpus"
    cat "${subsystem}/cpuset.mems" > "${subsystem}/${cgroup_root}/cpuset.mems"
  fi
  # We need to perform a self bind mount here because otherwise,
  # systemd might delete the cgroup unintentionally before the
  # kubelet starts.
  mount --bind "${subsystem}/${cgroup_root}" "${subsystem}/${cgroup_root}"
}

fix_cgroup() {
  echo 'INFO: fix cgroup mounts for all subsystems'
  # see: https://d2iq.com/blog/running-kind-inside-a-kubernetes-cluster-for-continuous-integration
  # capture initial state before
  local current_cgroup
  current_cgroup=$(grep systemd /proc/self/cgroup | cut -d: -f3)
  local cgroup_subsystems
  cgroup_subsystems=$(findmnt -lun -o source,target -t cgroup | grep "${current_cgroup}" | awk '{print $2}')
  # For each cgroup subsystem, Docker does a bind mount from the current
  # cgroup to the root of the cgroup subsystem. For instance:
  #   /sys/fs/cgroup/memory/docker/<cid> -> /sys/fs/cgroup/memory
  #
  # This will confuse Kubelet and cadvisor and will dump the following error
  # messages in kubelet log:
  #   `summary_sys_containers.go:47] Failed to get system container stats for ".../kubelet.service"`
  #
  # This is because `/proc/<pid>/cgroup` is not affected by the bind mount.
  # The following is a workaround to recreate the original cgroup
  # environment by doing another bind mount for each subsystem.
  local cgroup_mounts
  # xref: https://github.com/kubernetes/minikube/pull/9508
  # Example inputs:
  #
  # Docker:               /docker/562a56986a84b3cd38d6a32ac43fdfcc8ad4d2473acf2839cbf549273f35c206 /sys/fs/cgroup/devices rw,nosuid,nodev,noexec,relatime shared:143 master:23 - cgroup devices rw,devices
  # podman:               /libpod_parent/libpod-73a4fb9769188ae5dc51cb7e24b9f2752a4af7b802a8949f06a7b2f2363ab0e9 ...
  # Cloud Shell:          /kubepods/besteffort/pod3d6beaa3004913efb68ce073d73494b0/accdf94879f0a494f317e9a0517f23cdd18b35ff9439efd0175f17bbc56877c4 /sys/fs/cgroup/memory rw,nosuid,nodev,noexec,relatime master:19 - cgroup cgroup rw,memory
  # GitHub actions #9304: /actions_job/0924fbbcf7b18d2a00c171482b4600747afc367a9dfbeac9d6b14b35cda80399 /sys/fs/cgroup/memory rw,nosuid,nodev,noexec,relatime shared:263 master:24 - cgroup cgroup rw,memory
  cgroup_mounts=$(grep -E -o '/[[:alnum:]].* /sys/fs/cgroup.*.*cgroup' /proc/self/mountinfo || true)
  if [[ -n "${cgroup_mounts}" ]]; then
    local mount_root
    mount_root=$(echo "${cgroup_mounts}" | head -n 1 | cut -d' ' -f1)
    for mount_point in $(echo "${cgroup_mounts}" | cut -d' ' -f 2); do
      # bind mount each mount_point to mount_point + mount_root
      # mount --bind /sys/fs/cgroup/cpu /sys/fs/cgroup/cpu/docker/fb07bb6daf7730a3cb14fc7ff3e345d1e47423756ce54409e66e01911bab2160
      local target="${mount_point}${mount_root}"
      if ! findmnt "${target}"; then
        mkdir -p "${target}"
        mount --bind "${mount_point}" "${target}"
      fi
    done
  fi
  # kubelet will try to manage cgroups / pods that are not owned by it when
  # "nesting" clusters, unless we instruct it to use a different cgroup root.
  # We do this, and when doing so we must fixup this alternative root
  # currently this is hardcoded to be /kubelet
  mount --make-rprivate /sys/fs/cgroup
  echo "${cgroup_subsystems}" |
  while IFS= read -r subsystem; do
    mount_kubelet_cgroup_root "/kubelet" "${subsystem}"
  done
}

fix_cgroup
