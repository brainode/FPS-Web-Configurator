FROM ubuntu:24.04 AS warfork-dist

ARG DEBIAN_FRONTEND=noninteractive
ARG WARFORK_APP_ID=1136510

RUN dpkg --add-architecture i386 \
 && apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    libc6-i386 \
    lib32gcc-s1 \
    lib32stdc++6 \
    zlib1g:i386 \
 && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /opt/steamcmd \
 && curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz -o /tmp/steamcmd_linux.tar.gz \
 && tar -xzf /tmp/steamcmd_linux.tar.gz -C /opt/steamcmd \
 && rm /tmp/steamcmd_linux.tar.gz

# Warfork resolves correctly without platform overrides; forcing linux/64 can
# make SteamCMD fail with "Missing configuration" on some hosts.
RUN /opt/steamcmd/steamcmd.sh \
    +force_install_dir /opt/warfork \
    +login anonymous \
    +app_update ${WARFORK_APP_ID} validate \
    +quit

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

ENV WARFORK_INSTALL_DIR=/opt/warfork
ENV WARFORK_STEAM_RUNTIME_DIR=/opt/steam-runtime
ENV WARFORK_HOME_ROOT=/var/lib/warfork
ENV WARFORK_HOME_DIR=/var/lib/warfork/.local/share/warfork-2.1

WORKDIR /opt/warfork

COPY --from=warfork-dist /opt/warfork/ /opt/warfork/
COPY --from=warfork-dist /opt/steamcmd/linux32/steamclient.so /opt/steam-runtime/linux32/steamclient.so
COPY --from=warfork-dist /opt/steamcmd/linux64/steamclient.so /opt/steam-runtime/linux64/steamclient.so
COPY services/warfork-server/docker-entrypoint.sh /usr/local/bin/warfork-entrypoint
COPY services/warfork-server/healthcheck.sh /usr/local/bin/warfork-healthcheck

RUN chmod +x \
    /usr/local/bin/warfork-entrypoint \
    /usr/local/bin/warfork-healthcheck \
    /opt/warfork/wf_server.x86_64 \
    /opt/warfork/warfork.x86_64 \
    /opt/warfork/wftv_server.x86_64 \
 && mkdir -p \
    /opt/steam-runtime/linux32 \
    /opt/steam-runtime/linux64 \
    /var/lib/warfork/.local/share/warfork-2.1/basewf/docker \
    /var/lib/warfork/.local/share/warfork-2.1/basewf/configs/server/gametypes \
    /var/lib/warfork/.local/share/warfork-2.1/logs \
    /var/lib/warfork/.local/share/warfork-2.1/demos

VOLUME ["/var/lib/warfork"]

EXPOSE 44400/udp 44444/tcp

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD ["/usr/local/bin/warfork-healthcheck"]

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/warfork-entrypoint"]
CMD []
