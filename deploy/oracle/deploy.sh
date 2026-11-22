#!/usr/bin/env bash
# Chạy trên VM (workflow Deploy Oracle Server gọi qua SSH), trong thư mục mã nguồn đã giải nén của một tag:
#   bash deploy/oracle/deploy.sh v0.15.2
# Build image vandao-online:<tag>, chuyển nhãn current sang nó, khởi động lại app.
# /healthz không lên trong 3 phút → quay về tag trước đó và báo lỗi.
set -euo pipefail

TAG="${1:?Thiếu tag, vd v0.15.2}"
ROOT=/opt/vandao
SRC="$(cd "$(dirname "$0")/../.." && pwd)"

[ -f "$ROOT/.env" ] || { echo "Thiếu $ROOT/.env (xem deploy/oracle/env.example)" >&2; exit 1; }
[ -f "$ROOT/secrets/supabase-ca.crt" ] || { echo "Thiếu $ROOT/secrets/supabase-ca.crt" >&2; exit 1; }

echo "== Build vandao-online:$TAG"
docker build -f "$SRC/Dockerfile.online" -t "vandao-online:$TAG" "$SRC"

cp "$SRC/deploy/oracle/docker-compose.yml" "$SRC/deploy/oracle/Caddyfile" "$ROOT/"
prev="$(cat "$ROOT/current-tag" 2>/dev/null || true)"

healthy() {
  for _ in $(seq 1 36); do
    if curl -fsS -m 5 http://127.0.0.1:8080/healthz 2>/dev/null | grep -q '"ok"'; then return 0; fi
    sleep 5
  done
  return 1
}

switch_to() {
  docker tag "vandao-online:$1" vandao-online:current
  (cd "$ROOT" && docker compose up -d --remove-orphans --force-recreate app && docker compose up -d caddy)
}

echo "== Chạy $TAG (trước đó: ${prev:-chưa có})"
switch_to "$TAG"
if ! healthy; then
  echo "!! $TAG không qua /healthz" >&2
  (cd "$ROOT" && docker compose logs --tail 80 app) >&2 || true
  if [ -n "$prev" ] && [ "$prev" != "$TAG" ] && docker image inspect "vandao-online:$prev" >/dev/null 2>&1; then
    echo "!! Quay về $prev" >&2
    switch_to "$prev"
    healthy || echo "!! $prev cũng không lên, xem: cd $ROOT && docker compose logs app" >&2
  fi
  exit 1
fi
echo "$TAG" > "$ROOT/current-tag"
(cd "$ROOT" && docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile) || true

# Giữ 3 bản gần nhất (mã nguồn + image) để quay về nhanh
echo "== Dọn bản cũ"
keep=$(ls -1t "$ROOT/releases" | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -3 || true)
for d in $(ls -1 "$ROOT/releases"); do
  if ! grep -qx "$d" <<<"$keep"; then rm -rf "${ROOT:?}/releases/$d"; fi
done
for img in $(docker images vandao-online --format '{{.Tag}}' | grep -E '^v[0-9]' || true); do
  if ! grep -qx "$img" <<<"$keep" && [ "$img" != "$TAG" ]; then docker rmi "vandao-online:$img" >/dev/null || true; fi
done
docker image prune -f >/dev/null || true

echo "== Đã chạy $TAG"
