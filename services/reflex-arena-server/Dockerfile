ARG GAME_SERVER_PLATFORM=linux/amd64

FROM --platform=$GAME_SERVER_PLATFORM ubuntu:24.04

ARG DEBIAN_FRONTEND=noninteractive

RUN dpkg --add-architecture i386 \
 && apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    gosu \
    libcurl4 \
    libc6-i386 \
    lib32gcc-s1 \
    lib32stdc++6 \
    libgcc-s1 \
    libstdc++6 \
    procps \
    tini \
    zlib1g:i386 \
 && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /opt/steamcmd \
 && curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz -o /tmp/steamcmd_linux.tar.gz \
 && tar -xzf /tmp/steamcmd_linux.tar.gz -C /opt/steamcmd \
 && rm /tmp/steamcmd_linux.tar.gz

# Reflex needs an explicit Linux 64-bit target; otherwise SteamCMD may return
# "Missing configuration" during app_update.
RUN /opt/steamcmd/steamcmd.sh \
    +@sSteamCmdForcePlatformType linux \
    +@sSteamCmdForcePlatformBitness 64 \
    +force_install_dir /opt/reflex-arena \
    +login anonymous \
    +app_update 329740 validate \
    +quit

COPY services/reflex-arena-server/docker-entrypoint.sh /usr/local/bin/reflex-entrypoint

ENV REFLEX_INSTALL_DIR=/opt/reflex-arena
ENV REFLEX_DATA_DIR=/var/lib/reflex-arena

RUN chmod +x \
    /usr/local/bin/reflex-entrypoint \
    /opt/reflex-arena/reflexded \
 && groupadd --system reflex \
 && useradd --system --gid reflex --home-dir /var/lib/reflex-arena --create-home reflex \
 && mkdir -p /var/lib/reflex-arena \
 && chown -R reflex:reflex /opt/reflex-arena /var/lib/reflex-arena

VOLUME ["/var/lib/reflex-arena"]

EXPOSE 25787/udp 25787/tcp

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD pgrep -x reflexded >/dev/null || exit 1

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/reflex-entrypoint"]
