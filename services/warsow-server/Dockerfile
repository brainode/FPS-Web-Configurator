FROM debian:bookworm-slim AS warsow-dist

ARG DEBIAN_FRONTEND=noninteractive
ARG WARSOW_DIST_URL=https://warsow.net/warsow-2.1.2.tar.gz

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    tar \
 && rm -rf /var/lib/apt/lists/*

RUN set -eux; \
    mkdir -p /tmp/warsow-src /warsow-dist; \
    curl -fsSL "$WARSOW_DIST_URL" -o /tmp/warsow.tar.gz; \
    tar -xzf /tmp/warsow.tar.gz -C /tmp/warsow-src; \
    extracted_dir="$(find /tmp/warsow-src -mindepth 1 -maxdepth 1 -type d | head -n 1)"; \
    test -n "$extracted_dir"; \
    cp -a "$extracted_dir"/. /warsow-dist/; \
    rm -rf /tmp/warsow.tar.gz /tmp/warsow-src

FROM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    libcurl4 \
    libfreetype6 \
    libgcc-s1 \
    libjpeg62-turbo \
    libopenal1 \
    libpng16-16 \
    libstdc++6 \
    procps \
    tini \
    zlib1g \
 && rm -rf /var/lib/apt/lists/*

ENV WARSOW_INSTALL_DIR=/opt/warsow
ENV WARSOW_HOME_ROOT=/var/lib/warsow
ENV WARSOW_HOME_DIR=/var/lib/warsow/.local/share/warsow-2.1

WORKDIR /opt/warsow

COPY --from=warsow-dist /warsow-dist/ /opt/warsow/
COPY services/warsow-server/docker-entrypoint.sh /usr/local/bin/warsow-entrypoint
COPY services/warsow-server/healthcheck.sh /usr/local/bin/warsow-healthcheck

RUN chmod +x \
    /usr/local/bin/warsow-entrypoint \
    /usr/local/bin/warsow-healthcheck \
    /opt/warsow/wsw_server \
    /opt/warsow/warsow \
    /opt/warsow/wswtv_server \
    /opt/warsow/wsw_server.x86_64 \
    /opt/warsow/warsow.x86_64 \
 && mkdir -p \
    /var/lib/warsow/.local/share/warsow-2.1/basewsw/docker \
    /var/lib/warsow/.local/share/warsow-2.1/basewsw/configs/server/gametypes \
    /var/lib/warsow/.local/share/warsow-2.1/logs \
    /var/lib/warsow/.local/share/warsow-2.1/demos

VOLUME ["/var/lib/warsow"]

EXPOSE 44400/udp 44444/tcp

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD ["/usr/local/bin/warsow-healthcheck"]

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/warsow-entrypoint"]
CMD []
