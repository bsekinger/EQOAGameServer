FROM alpine:3.17.0

RUN apk --no-cache add dnsmasq

VOLUME /etc/dnsmasq

EXPOSE 53 53/udp

ENTRYPOINT ["dnsmasq", "-k"]
