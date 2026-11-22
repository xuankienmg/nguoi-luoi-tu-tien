#!/usr/bin/env bash
# Dựng VM Oracle Cloud (Ubuntu 22.04/24.04, ARM Ampere A1 hoặc AMD) cho backend online. Chạy một lần, chạy lại vẫn an toàn:
#   sudo bash setup-vm.sh
# Việc làm: cài Docker + Compose từ kho Ubuntu, xoay log Docker, mở cổng 80/443 trong iptables của VM,
# tạo /opt/vandao cho user deploy (mặc định ubuntu), thêm swap nếu RAM < 4 GB.
# Cổng 80/443 còn phải mở ở Security List / NSG của VCN trên console Oracle (tools/online_cloud/ORACLE.md).
set -euo pipefail

DEPLOY_USER="${DEPLOY_USER:-ubuntu}"
ROOT=/opt/vandao

[ "$(id -u)" -eq 0 ] || { echo "Chạy bằng sudo: sudo bash $0" >&2; exit 1; }
id "$DEPLOY_USER" >/dev/null 2>&1 || { echo "Không có user $DEPLOY_USER (đặt DEPLOY_USER=...)" >&2; exit 1; }

export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y docker.io curl iptables-persistent
# Compose v2: Ubuntu 24.04 có gói docker-compose-v2; bản cũ hơn dùng docker-compose-plugin nếu có
apt-get install -y docker-compose-v2 || apt-get install -y docker-compose-plugin
docker compose version

# Xoay log mặc định để đĩa (Always Free: boot volume ~47 GB) không đầy
if [ ! -f /etc/docker/daemon.json ]; then
  cat > /etc/docker/daemon.json <<'JSON'
{ "log-driver": "json-file", "log-opts": { "max-size": "20m", "max-file": "5" } }
JSON
fi
systemctl enable --now docker
systemctl restart docker
usermod -aG docker "$DEPLOY_USER"

# Ảnh Ubuntu của Oracle có luật REJECT cuối chuỗi INPUT: chèn luật ACCEPT trước nó
open_port() {  # $1 = tcp|udp, $2 = cổng
  if iptables -C INPUT -p "$1" --dport "$2" -m conntrack --ctstate NEW -j ACCEPT 2>/dev/null; then return; fi
  local n
  n=$(iptables -L INPUT --line-numbers -n | awk '$2=="REJECT"{print $1; exit}')
  if [ -n "$n" ]; then
    iptables -I INPUT "$n" -p "$1" --dport "$2" -m conntrack --ctstate NEW -j ACCEPT
  else
    iptables -A INPUT -p "$1" --dport "$2" -m conntrack --ctstate NEW -j ACCEPT
  fi
}
open_port tcp 80
open_port tcp 443
open_port udp 443
netfilter-persistent save

# Swap cho VM nhỏ (vd AMD 1 GB): dotnet publish trong docker build cần nhiều RAM
mem_kb=$(awk '/MemTotal/{print $2}' /proc/meminfo)
if [ "$mem_kb" -lt 4000000 ] && ! swapon --show | grep -q /swapfile; then
  fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

install -d -o "$DEPLOY_USER" -g "$DEPLOY_USER" -m 755 "$ROOT" "$ROOT/releases"
install -d -o "$DEPLOY_USER" -g "$DEPLOY_USER" -m 700 "$ROOT/secrets"

echo
echo "Xong. Việc còn lại (tools/online_cloud/ORACLE.md):"
echo "  1. Tạo $ROOT/.env từ deploy/oracle/env.example, chmod 600."
echo "  2. Đặt chứng chỉ CA Supabase vào $ROOT/secrets/supabase-ca.crt, chmod 644."
echo "  3. Đăng xuất / đăng nhập lại để user $DEPLOY_USER dùng được docker."
echo "  4. Chạy workflow 'Deploy Oracle Server' trên GitHub."
