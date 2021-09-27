FROM nginx:1

ADD etc/*.conf /etc/nginx/
ADD etc/api_conf.d/*.conf /etc/nginx/api_conf.d/
ADD etc/backend_conf.d/*.conf /etc/nginx/backend_conf.d/
RUN rm /etc/nginx/conf.d/default.conf
