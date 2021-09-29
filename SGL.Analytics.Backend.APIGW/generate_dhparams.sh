#/bin/bash
if [ ! -f dhparams.pem ]; then
	openssl dhparam -out dhparams.pem 4096
fi
