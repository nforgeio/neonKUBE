# neon-cli: 1.2.84 (DEV)

## Changes

This is a major development release including major BREAKING changes to all librares as well as **neon-cli** and cluster services.

The list below includes the highlights:

* Implemented and tested most of the built-in Ansible modules.
* 

## Upgrade Steps

You'll need to perform the following steps to upgrade successfully:

1. **XenServer**: Manually delete any `neon-template` templates on each of your Xen host machines and rebuild all clusters from scratch.

2. **Hyper-V: Run the command below on all of your devops and development workstations to remove any cached VM images and rebuild all clusters from scratch:

`neon cluster prepare --remove-templates`

3. Edit `C:\Windows\System32\drivers\etc\hosts` as administrator and remove any DNS definitions in any temporary sections.
