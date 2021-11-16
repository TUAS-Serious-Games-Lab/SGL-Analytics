#!/bin/sh
# FQDN-retrieval based on https://unix.stackexchange.com/a/477274
MY_CERT_FQDN=$(openssl x509 -noout -subject -in /etc/ssl/my/mycert.crt | awk -F= '{print $NF}' | sed -e 's/^[ \t]*//')
(curl --fail --no-progress-meter --cacert /etc/ssl/my/mycert.crt https://${MY_CERT_FQDN}/check ) || exit 1
