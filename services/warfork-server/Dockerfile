ARG GAME_SERVER_PLATFORM=linux/amd64

FROM --platform=$GAME_SERVER_PLATFORM registry.gitlab.steamos.cloud/steamrt/sniper/sdk:latest AS warfork-custom-build

ARG DEBIAN_FRONTEND=noninteractive
ARG WARFORK_QFUSION_REPO=https://github.com/TeamForbiddenLLC/warfork-qfusion.git
ARG WARFORK_QFUSION_REF=756ba9fd529f59eecb2c38e9fc93f3b2d79b0aba

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    clang-11 \
    cmake \
    curl \
    git \
    make \
    unzip \
    zip \
 && rm -rf /var/lib/apt/lists/*

RUN set -eux; \
    git clone --filter=blob:none "$WARFORK_QFUSION_REPO" /src/warfork-qfusion; \
    git -C /src/warfork-qfusion fetch --depth 1 origin "$WARFORK_QFUSION_REF"; \
    git -C /src/warfork-qfusion checkout --detach FETCH_HEAD; \
    git -C /src/warfork-qfusion submodule update --init --depth 1 \
        source/extern/SDL \
        source/extern/ogg \
        source/extern/vorbis \
        source/extern/tracy

COPY services/warfork-server/engine-patches/ /tmp/warfork-engine-patches/

RUN set -eux; \
    for patch in /tmp/warfork-engine-patches/*.patch; do \
        git -C /src/warfork-qfusion apply "$patch"; \
    done

RUN set -eux; \
    cd /src/warfork-qfusion/source; \
    CC=clang-11 CXX=clang++-11 cmake -B ./build \
        -DCMAKE_BUILD_TYPE=RelWithDebInfo \
        -DBUILD_STEAMLIB=0 \
        -DUSE_CRASHPAD=0 \
        -DUSE_GRAPHICS_NRI=0 \
        -DUSE_SYSTEM_ZLIB=1 \
        -DUSE_SYSTEM_OPENAL=1 \
        -DUSE_SYSTEM_CURL=1 \
        -DUSE_SYSTEM_FREETYPE=1; \
    cmake --build ./build --target wf_server --parallel "$(nproc)"; \
    cmake --build ./build --target deploy

FROM --platform=$GAME_SERVER_PLATFORM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    libcurl3-gnutls \
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

COPY --from=warfork-custom-build /src/warfork-qfusion/source/build/warfork-qfusion/ /opt/warfork/
COPY services/warfork-server/gametypes/ /usr/local/share/warfork-custom-gametypes/
COPY services/warfork-server/docker-entrypoint.sh /usr/local/bin/warfork-entrypoint
COPY services/warfork-server/healthcheck.sh /usr/local/bin/warfork-healthcheck

RUN sed -i 's/\r$//' \
    /usr/local/bin/warfork-entrypoint \
    /usr/local/bin/warfork-healthcheck \
    /usr/local/share/warfork-custom-gametypes/* \
 && chmod +x \
    /usr/local/bin/warfork-entrypoint \
    /usr/local/bin/warfork-healthcheck \
    /opt/warfork/wf_server.x86_64 \
 && mkdir -p \
    /opt/steam-runtime/linux32 \
    /opt/steam-runtime/linux64 \
    /usr/local/share/warfork-custom-gametypes \
    /var/lib/warfork/.local/share/warfork-2.1/basewf/docker \
    /var/lib/warfork/.local/share/warfork-2.1/basewf/configs/server/gametypes \
    /var/lib/warfork/.local/share/warfork-2.1/basewf/progs/gametypes \
    /var/lib/warfork/.local/share/warfork-2.1/logs \
    /var/lib/warfork/.local/share/warfork-2.1/demos

VOLUME ["/var/lib/warfork"]

EXPOSE 44400/udp 44444/tcp

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD ["/usr/local/bin/warfork-healthcheck"]

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/warfork-entrypoint"]
CMD []
