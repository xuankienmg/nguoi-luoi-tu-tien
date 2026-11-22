# Backend online trên Oracle Cloud Always Free (chạy song song Render)

Mục tiêu: chạy backend online trên VM miễn phí của Oracle, **giữ Render chạy song song** cho đến khi Oracle ổn định, rồi mới chuyển hẳn.

Hai backend dùng chung database Supabase. Chạy cùng lúc vẫn đúng: mỗi tài khoản bị khóa advisory trong Postgres (`OnlineBoundary`), `PlayerCache` phát hiện ghi từ process khác qua `xmin` và nạp lại. Client đã phát hành vẫn trỏ về Render (`Online:DefaultBackendUrl`); Oracle chỉ nhận người chơi khi bạn chủ động trỏ về.

File liên quan: `deploy/oracle/` (compose, Caddyfile, script dựng VM, script deploy), workflow `.github/workflows/deploy-oracle.yml`.

Không gửi mật khẩu, key, connection string qua chat hoặc commit vào repo.

## 1. Tạo VM

1. Đăng ký Oracle Cloud, **Home Region chọn Singapore** (gần Supabase; Home Region không đổi được sau này, VM Always Free chỉ tạo ở Home Region).
2. Compute → Instances → Create:
   - Image: **Canonical Ubuntu 24.04** (bản aarch64 cho máy ARM).
   - Shape: **VM.Standard.A1.Flex** (Ampere ARM), ví dụ 2 OCPU / 12 GB (hạn mức Always Free tổng cộng 4 OCPU / 24 GB). Báo hết máy (*Out of capacity*) thì thử lại lúc khác hoặc đổi Availability Domain.
   - Dán SSH public key của bạn để đăng nhập.
3. Networking → IP public: nên đổi sang **Reserved Public IP** (nếu tài khoản cho phép) để IP không đổi khi stop/start VM.
4. VCN → Security List (hoặc NSG) của subnet → **Ingress Rules**: thêm TCP 80 và TCP 443 (và UDP 443 cho HTTP/3), nguồn `0.0.0.0/0`.
5. Oracle có thể thu hồi VM Always Free để rảnh lâu (CPU rất thấp nhiều ngày). Nâng tài khoản lên *Pay As You Go* thì không bị thu hồi và vẫn miễn phí nếu ở trong hạn mức Always Free; nên đặt Budget alert 1 USD để chắc chắn.

## 2. Tên miền

Tạo bản ghi **A** `online.<tên-miền>` → IP public của VM. Nếu dùng Cloudflare, để **DNS only** (mây xám) để Caddy tự xin chứng chỉ Let's Encrypt.

Dùng tên miền riêng thì sau này đổi host chỉ cần đổi DNS, không phải phát hành lại client.

## 3. Dựng VM

```bash
ssh ubuntu@<IP>
# chép deploy/oracle/setup-vm.sh lên VM (scp hoặc dán nội dung), rồi:
sudo bash setup-vm.sh
exit   # đăng nhập lại để user ubuntu vào nhóm docker
```

Script cài Docker + Compose (kho Ubuntu), xoay log, mở cổng 80/443 trong iptables của VM (ảnh Ubuntu của Oracle chặn mặc định), tạo `/opt/vandao`.

Sau đó trên VM:

```bash
cd /opt/vandao
nano .env            # nội dung theo deploy/oracle/env.example, cùng giá trị đang dùng trên Render
chmod 600 .env
nano secrets/supabase-ca.crt   # chứng chỉ CA Supabase (prod-ca-2021.crt), giống Secret File của Render
chmod 644 secrets/supabase-ca.crt
```

- `DOMAIN` và `Online__PublicUrl` dùng tên miền Oracle (`https://online.<tên-miền>`), không phải URL Render.
- `ConnectionStrings__Game`: giữ nguyên chuỗi session pooler đang dùng trên Render (host direct của Supabase chỉ có IPv6).

## 4. Supabase Auth

**Authentication → URL Configuration → Redirect URLs:** **thêm** (không xóa URL Render):

- `https://online.<tên-miền>/online-reset.html`
- `https://online.<tên-miền>/online-login.html`

Site URL giữ là URL Render cho tới khi chuyển hẳn.

## 5. GitHub

Thêm vào environment **Supbase** của repo (cùng chỗ với `RENDER_DEPLOY_HOOK`):

| Loại | Tên | Giá trị |
|---|---|---|
| Secret | `ORACLE_HOST` | IP public (hoặc tên miền) của VM |
| Secret | `ORACLE_SSH_KEY` | private key **riêng cho deploy** (tạo mới bằng `ssh-keygen -t ed25519`, public key thêm vào `~/.ssh/authorized_keys` của ubuntu trên VM) |
| Secret | `ORACLE_KNOWN_HOSTS` | kết quả `ssh-keyscan -t ed25519 <IP>` chạy từ máy bạn (so khớp fingerprint với VM) |
| Variable | `ORACLE_PUBLIC_URL` | `https://online.<tên-miền>` |
| Variable | `ORACLE_USER` | tuỳ chọn, mặc định `ubuntu` |

## 6. Deploy

- Lần đầu: Actions → **Deploy Oracle Server** → Run workflow với tag đang chạy trên Render (vd `v0.15.2`). Đánh dấu *migrations_applied* nếu tag đó có migration mới và đã áp lên Supabase.
- Các lần sau: **Build & Release** tự gọi cả Render và Oracle. Thiếu secret `ORACLE_*` thì bước Oracle tự bỏ qua.

Workflow đóng gói mã nguồn của tag (`git archive`), đẩy lên VM qua SSH, `deploy/oracle/deploy.sh` build image trên VM, khởi động lại app và chờ `/healthz`. Không qua healthz trong 3 phút thì tự quay về tag trước.

## 7. Kiểm tra

- `https://online.<tên-miền>/healthz` trả `ok`.
- Mở `https://online.<tên-miền>/` để chơi bản web: đăng nhập, đồng bộ, Chợ, Lôi đài, chat, quên mật khẩu.
- Thử bản desktop: sửa `Online:DefaultBackendUrl` trong `appsettings.json` **của bản cài trên máy bạn** sang URL Oracle.

Lệnh hay dùng trên VM:

```bash
cd /opt/vandao
docker compose ps
docker compose logs -f app       # log backend
docker compose logs caddy        # lỗi chứng chỉ / cổng
cat current-tag                  # tag đang chạy
```

Quay về bản trước bằng tay: `docker tag vandao-online:<tag-cũ> vandao-online:current && docker compose up -d --force-recreate app && echo <tag-cũ> > current-tag`.

## 8. Chuyển hẳn (khi Oracle đã ổn định vài tuần)

1. Đổi `Online:DefaultBackendUrl` trong `appsettings.json` sang `https://online.<tên-miền>`, phát hành bản mới.
2. Supabase Auth: Site URL sang tên miền Oracle.
3. Giữ Render thêm một thời gian cho người chơi chưa cập nhật (có thể nâng `min_version` để buộc cập nhật).
4. Sau đó tắt service Render, bỏ job `deploy-server` trong `build-release.yml`, xóa `render.yaml` và secret `RENDER_DEPLOY_HOOK`.
