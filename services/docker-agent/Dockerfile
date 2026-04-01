FROM python:3.12-alpine

RUN apk add --no-cache docker-cli

WORKDIR /app

COPY services/docker-agent/agent.py .

ENV DOCKER_AGENT_PORT=8081
# DOCKER_HOST is intentionally not set here so it defaults to
# the socket mount (/var/run/docker.sock).
# Override via .env if using Docker Desktop TCP endpoint:
#   DOCKER_HOST=tcp://host.docker.internal:2375

EXPOSE 8081

HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1:${DOCKER_AGENT_PORT}/health/live || exit 1

ENTRYPOINT ["python3", "-u", "agent.py"]
