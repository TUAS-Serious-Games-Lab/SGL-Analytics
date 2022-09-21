#!/bin/bash
#Increments the last digit of the version number if it is a pre-release version.
#Split into version number and pre-release suffix:
MAIN_VERSION=$(echo $1 | cut -d '-' -f 1)
PRERELEASE_SUFFIX=$(echo $1 | cut -d '-' -s -f 1 --complement)
if [ -z "$PRERELEASE_SUFFIX" ]
then
	# If there is no pre-release suffix, use version number as-is.
	echo "$MAIN_VERSION"
else
	# If there is a suffix, increment version to next one at last digit,
	# as suffixed versions are after the tag in git but SemVer sees them as before the non-suffix version,
	# as they are candidates for it.
	INCREMENTED_VERSION=$(echo $MAIN_VERSION | awk -F. -v OFS=. '{$NF += 1 ; print}') # From https://stackoverflow.com/a/61921674
	# recombine the version
	echo "${INCREMENTED_VERSION}-${PRERELEASE_SUFFIX}"
fi
