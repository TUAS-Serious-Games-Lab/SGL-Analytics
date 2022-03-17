#!/bin/bash
# Scans backward through commit date ordered tag list to find a tag based upon which the current version can be described
TAGS=$(git tag --sort=committerdate | grep -P '^\d+(\.\d+)*$' | tac)
for TAG in $TAGS; do
	git describe --tags --match $TAG 2>/dev/null && exit 0
done
# If no tag worked to describe the version, error out
>&2 echo "Error: Couldn't find version from tags!"
exit 1
