#!/bin/bash
git tag --sort=committerdate | grep -P '^\d+(\.\d+)*$' | tail -n1 | xargs git describe --tags --match
