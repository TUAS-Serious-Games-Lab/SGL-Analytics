#!/bin/sh
set -e
if [[ -f /etc/ssl/my/mycert.crt ]] && [[ -f /etc/ssl/my/mycert.key ]]; then
	echo "${0}: VS certificate is already converted, launching nginx entrypoint..."
	exec /docker-entrypoint.sh "$@"
	exit
fi
if [[ -z "${VS_CERT_SECRET_ID}" ]]; then
	echo "VS_CERT_SECRET_ID not set!"
	exit 1
fi
if [[ -z "${VS_CERT_NAME}" ]]; then
	echo "VS_CERT_NAME not set!"
	exit 2
fi
python3 -c "exit(0)" || apk add --no-cache python3
openssl version || apk add --no-cache openssl
echo "${0}: Reading password for VS HTTPS certificate..."
cd /root/.microsoft/usersecrets/${VS_CERT_SECRET_ID}
PRIVATE_KEY_PASS=$(python3 -c "import sys, json, codecs; print(json.load(codecs.open('secrets.json', 'r', 'utf-8-sig'))['Kestrel:Certificates:Development:Password'])")
cd /root/.aspnet/https
export PRIVATE_KEY_PASS
echo "${0}: Converting VS HTTPS certificate and its key..."
openssl pkcs12 -in ./${VS_CERT_NAME}.pfx -clcerts -nokeys -out /etc/ssl/my/mycert.crt -passin env:PRIVATE_KEY_PASS
openssl pkcs12 -in ./${VS_CERT_NAME}.pfx -nocerts -nodes -out /etc/ssl/my/mycert.key -passin env:PRIVATE_KEY_PASS
export -n PRIVATE_KEY_PASS
echo "${0}: Launching nginx entrypoint..."
exec /docker-entrypoint.sh "$@"
