FROM nginx:1

# Users of the image are expected to mount a volume containing the tls certificate and its key to /etc/ssl/my

ADD etc/*.conf /etc/nginx/
ADD etc/api_conf.d/*.conf /etc/nginx/api_conf.d/
ADD etc/backend_conf.d/*.conf /etc/nginx/backend_conf.d/
ADD use_vs_cert.sh /use_vs_cert.sh
RUN rm /etc/nginx/conf.d/default.conf &&\
	chmod +x /use_vs_cert.sh
