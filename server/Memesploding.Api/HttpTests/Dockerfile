# syntax=docker/dockerfile:1

FROM alpine:3.20

RUN apk add --no-cache bash curl jq dos2unix

WORKDIR /tests

# Copy the http-test suite into the image (no bind-mount needed)
COPY server/Memesploding.Api/HttpTests/ ./

# Normalize line endings + make scripts executable
RUN dos2unix *.sh wait-for-api.sh 2>/dev/null || true \
    && chmod +x *.sh wait-for-api.sh

ENV BASE_URL=http://api:5217/api/v1

CMD ["bash", "-lc", "./wait-for-api.sh && ./run-integration-tests-v2.sh"]
