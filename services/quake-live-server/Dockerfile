FROM ubuntu:22.04

ARG DEBIAN_FRONTEND=noninteractive

RUN dpkg --add-architecture i386 \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    gosu \
    libgcc-s1 \
    libstdc++6 \
    libc6-i386 \
    lib32gcc-s1 \
    lib32stdc++6 \
    netcat-openbsd \
    tar \
    tini \
    zlib1g:i386 \
 && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /opt/steamcmd \
 && curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz -o /tmp/steamcmd_linux.tar.gz \
 && tar -xzf /tmp/steamcmd_linux.tar.gz -C /opt/steamcmd \
 && rm /tmp/steamcmd_linux.tar.gz

RUN /opt/steamcmd/steamcmd.sh \
    +@sSteamCmdForcePlatformType linux \
    +@sSteamCmdForcePlatformBitness 32 \
    +force_install_dir /opt/quake-live \
    +login anonymous \
    +app_update 349090 validate \
    +quit

COPY services/quake-live-server/docker-entrypoint.sh /usr/local/bin/ql-entrypoint

ENV QL_INSTALL_DIR=/opt/quake-live
ENV QL_HOME_DIR=/var/lib/quake-live

RUN chmod +x \
    /usr/local/bin/ql-entrypoint \
    /opt/quake-live/run_server_x86.sh \
    /opt/quake-live/run_server_x64.sh \
 && groupadd --system quakelive \
 && useradd --system --gid quakelive --home-dir /var/lib/quake-live --create-home quakelive \
 && chown -R quakelive:quakelive /opt/quake-live

VOLUME ["/var/lib/quake-live"]

EXPOSE 27960/udp 27960/tcp 28960/tcp

HEALTHCHECK --interval=30s --timeout=5s --start-period=90s --retries=3 \
    CMD nc -zu 127.0.0.1 27960 || exit 1

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/ql-entrypoint"]
