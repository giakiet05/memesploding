# memesploding

## Backend (Docker)

### Start BE stack (API + Game + Postgres + Redis)

```bash
docker compose up --build -d
```

API: http://localhost:5217

### Run HTTP integration tests (in Docker)

This uses the existing test suite in `server/Memesploding.Api/HttpTests/`.

```bash
docker compose --profile test up --build --abort-on-container-exit http-tests
```

### Stop & cleanup

```bash
docker compose down -v
```
