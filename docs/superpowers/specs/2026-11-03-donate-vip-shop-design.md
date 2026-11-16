# Donate codes and Tạp Hóa VIP

Approved in chat (2026-11-03). Players donate by MoMo transfer at **1,000 VND = 1 Linh Thạch Thượng Phẩm** (`thuong_pham_linh_thach`). An admin turns each donation into a single-use code; the player redeems it in game. Thượng Phẩm is spent at a new **Tạp Hóa VIP** next to Tạp Hóa bà Diem. Automatic payment confirmation (VietQR + SePay/Casso webhook) is out of scope for now and can later reuse the same code store.

## Donate codes

- **Store:** new table `game.DonateCodes` (EF entity `DonateCode`, online migration + regenerated `supabase/migrations/20261026_online_authority.sql`):
  `Code` (PK, upper-case), `Amount` (Thượng Phẩm, int > 0), `Vnd` (long), `Note` (≤ 100 chars), `CreatedBy` (Supabase user id), `CreatedAt`, `UsedAt` (null = unused), `UsedByPlayer` (Guid?), `UsedByOwner` (Guid?, online account), `Offline` (bool).
- **Format:** `NLTT-XXXX-XXXX`, 8 random chars from an alphabet without look-alikes (no 0/O/1/I), from `RandomNumberGenerator`.
- **Create (admin only):** online Server, account in `Online:AdminUsers`. `POST /api/players/{id}/admin/donate-code?vnd=50000&note=...` → creates a code worth `vnd / 1000` (vnd must be a positive multiple of 1,000, at most 10,000,000). `GET` list of the latest 50 codes with status. Shown in the Admin tab, section **Donate**: amount + note inputs, copy button, list (code, amount, note, created, used by / when).
- **Redeem online:** the existing gift-code box. `RedeemCode` recognises the `NLTT-` prefix and, in Server mode, marks the row used with a conditional update (`UsedAt IS NULL`) inside the player's request transaction, then adds Thượng Phẩm and logs "Nhận N Linh Thạch Thượng Phẩm từ code donate". A used or unknown code → "Code không đúng hoặc đã được dùng."
- **Redeem offline:** the offline game calls the backend `POST /api/donate/redeem` (public, no account) with `{code, player}`. The backend marks the code used (`Offline = true`, `UsedByPlayer`) and returns `{amount}`; the offline game then adds the stones to the local save and logs the same line. Rate limit per IP: 10 attempts per minute (same limiter pattern as `online-auth`). Network failure → "Không kết nối được máy chủ để kiểm tra code." and nothing is added.
- **Item text:** `thuong_pham_linh_thach` loses "Chưa mở"; `Use` explains donate codes and Tạp Hóa VIP. Chợ keeps it admin-only (`AdminOnlyTrade`).

## Tạp Hóa VIP

- New shop def `GameData.VipShop` (`id = "vip"`, name "Tạp Hóa VIP"), shown in Chợ next to Tạp Hóa bà Diem, paid in Thượng Phẩm. Fixed list, **no weekly limits** (changed in chat 2026-11-03); only Gói Tốc Hành is once per character.
- Server-side validation like `ShopBuy`: item in list, quantity 1–99, enough Thượng Phẩm, bag space.

| Group | Item | Price (Thượng Phẩm) | Limit |
|---|---|---|---|
| Spin tickets | Vé vòng quay (`ve_tai_xiu`) | 1 | — |
| | Vé Linh Thú (`ve_linh_thu`) | 1 | — |
| Costumes | each `ruong_skin_*` chest (6 female sets) | 10 | — |
| Tinh Thạch | every gem type, level 7 | 10 | — |
| | level 8 | 20 | — |
| | level 9 | 30 | — |
| | level 10 | 50 | — |
| Spirit beasts | Thiên Thú Đản (`thu_dan_thien`) | 30 | — |
| | Thượng Cổ Thú Đản (`thu_dan_thuong_co`) | 50 | — |
| | Linh Thú Đan ×10 (`linh_thu_dan`) | 5 | — |
| Herbs | any spirit herb of tier ≤ current realm tier + 1, player's choice | 1 per herb | — |
| Utility | Thẻ đổi tên (`the_doi_ten`) | 20 | — |
| | Tụ Linh Đan, Bế Quan Đan, Thượng phẩm grade | 3 each | — |
| | Exchange 1 Thượng Phẩm → linh thạch | 1 | — |
| Pack | **Gói Tốc Hành** (one-time): unlock exploration ×2 + 10 `bo_nguyen_dan_trung` + 10 `bo_nguyen_dan_thuong` | 100 | once per character |

- **Herb exchange:** herb tier = N in the herb description "Linh thảo bậc N". Realm tier = `max(1, realm index)` capped at the highest herb tier (Luyện Khí and Trúc Cơ = 1, Kim Đan = 2, Nguyên Anh = 3 …), matching which pills the herbs brew. Allowed herbs: tier ≤ realm tier + 1. The player picks the herb and quantity (1–99); each herb costs 1 Thượng Phẩm. A Luyện Khí player can pick tier 1–2 herbs; a Kim Đan player tier 1–3.
- **Gói Tốc Hành (×2 exploration):** bought once (`Player.Speed2` = unlocked, `Player.Speed2On` = toggle; offline schema upgrade + online migration). Toggle endpoint `POST /api/players/{id}/speed2?on=true|false`, rejected if not unlocked. While on, every exploration trip takes half the time (same effect as the admin `fast2` flag, stacking rules unchanged: admin flags still override). A **×2 toggle button** on the main screen next to the exploration bar, shown only after the pack is bought; the tooltip says on/off. The offer shows "Đã mở" once bought.

- **Exchange rate:** 1 Thượng Phẩm = `20 × KyDuyen.StoneUnit(realm)` linh thạch (Luyện Khí 1,000 · Trúc Cơ 5,000 · Kim Đan 25,000 · Nguyên Anh 125,000 · Hóa Thần 625,000 …), shown on the offer.
- Pet gear is not sold in this version.
- **UI:** a second shop card/tab "Tạp Hóa VIP" in the Chợ pane with the Thượng Phẩm balance, grouped offers, price with the Thượng Phẩm icon, and "Đã mở" on the pack once bought. English strings added to `i18n-en.js`.

## Tests

- Code creation: admin only, amount from VND, format, rejects non-multiples of 1,000.
- Redeem online: adds stones once; second use and unknown code fail; concurrent double use credits once.
- Redeem offline endpoint: marks used with `Offline`, rate limit, unknown/used code.
- VIP shop: price/currency, insufficient Thượng Phẩm, bag full, exchange rate by realm, item not in list.
- Herb exchange: allowed tiers by realm, rejects a tier too high, quantity cost.
- Gói Tốc Hành: one purchase only, grants the pills, toggle halves trip time only when on, survives reload.
