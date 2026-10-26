  -- Vấn Đạo · Chợ giao dịch giữa người chơi trên Supabase (dùng chung project với bảng xếp hạng).
  -- Chạy một lần trong Supabase → SQL Editor. Chạy lại an toàn.
  --
  -- Mô hình ký gửi (escrow): treo bán → món rời túi người bán và nằm trên Supabase;
  -- mua → tin chuyển "sold" trong một lệnh UPDATE nguyên tử (không thể hai người cùng mua);
  -- người bán nhận linh thạch (đã trừ thuế) hoặc nhận lại đồ (hủy / hết hạn) qua market_claim.
  -- Anon key chỉ đọc được view market_open và gọi các hàm market_*; mọi hàm đều kiểm tra secret của nhân vật.

  -- ---------- Luật chợ (sửa ở đây rồi chạy lại file) ----------
  create or replace function public.market_rules()
  returns table(min_realm int, tax_pct numeric, max_open int, max_daily int, listing_days int)
  language sql immutable as $$ select 1, 5::numeric, 5, 10, 3 $$;

  create table if not exists public.market_players (
    player_id   uuid primary key,
    secret_hash text not null,
    name        text not null default '',
    banned      boolean not null default false,     -- chặn thủ công người gian lận (Table Editor)
    created_at  timestamptz not null default now()
  );

  alter table public.market_players add column if not exists unlimited_listings boolean not null default false;

  create table if not exists public.market_listings (
    id             bigint generated always as identity primary key,
    seller_id      uuid   not null references public.market_players(player_id),
    seller_name    text   not null,
    item_id        text   not null,
    qty            int    not null check (qty between 1 and 9999),
    price          bigint not null check (price between 1 and 2000000000),   -- tổng giá cả chồng
    status         text   not null default 'open' check (status in ('open', 'sold', 'cancelled', 'expired')),
    buyer_id       uuid,
    buyer_name     text,
    payout         bigint not null default 0,          -- người bán thực nhận (giá − thuế)
    seller_claimed boolean not null default false,
    created_at     timestamptz not null default now(),
    expires_at     timestamptz not null,
    closed_at      timestamptz
  );
  create index if not exists market_open_idx   on public.market_listings (status, created_at desc);
  create index if not exists market_seller_idx on public.market_listings (seller_id, status);
  create index if not exists market_buyer_idx  on public.market_listings (buyer_id, closed_at desc);

  alter table public.market_players  enable row level security;
  alter table public.market_listings enable row level security;
  revoke all on public.market_players, public.market_listings from anon, authenticated;

  create or replace view public.market_open as
  select l.id, l.seller_id, l.seller_name, l.item_id, l.qty, l.price, l.created_at, l.expires_at
  from public.market_listings l
  join public.market_players p on p.player_id = l.seller_id and not p.banned
  where l.status = 'open' and l.expires_at > now()
    -- người bán bị gắn cờ trên bảng xếp hạng (từng mở bằng bản game cũ): ẩn tin khỏi Chợ
    and not exists (select 1 from public.leaderboard lb where lb.player_id = l.seller_id and lb.flagged);
  grant select on public.market_open to anon, authenticated;

  -- ---------- Nội bộ: xác thực nhân vật bằng secret ----------
  create or replace function public.market_auth(p_id uuid, p_secret text, p_name text)
  returns void language plpgsql security definer set search_path = public as $$
  declare h text; r public.market_players;
  begin
    perform public.require_version();      -- docs/version/supabase.sql: chặn bản game cũ
    if p_secret is null or length(p_secret) < 24 then raise exception 'Khóa nhân vật không hợp lệ.'; end if;
    h := encode(sha256(convert_to(p_secret, 'UTF8')), 'hex');
    select * into r from public.market_players where player_id = p_id;
    if not found then
      insert into public.market_players (player_id, secret_hash, name) values (p_id, h, left(coalesce(p_name, ''), 24));
    elsif r.secret_hash <> h then raise exception 'Sai khóa nhân vật.';
    elsif r.banned then raise exception 'Nhân vật đã bị cấm giao dịch.';
    elsif p_name is not null and r.name <> left(p_name, 24) then
      update public.market_players set name = left(p_name, 24) where player_id = p_id;
    end if;
  end $$;

  create or replace function public.market_check_limits(p_id uuid, p_realm int)
  returns void language plpgsql security definer set search_path = public as $$
  declare k record; n int;
  begin
    select * into k from public.market_rules();
    if p_realm < k.min_realm then raise exception 'Cần đạt Trúc Cơ mới giao dịch ở Chợ.'; end if;
    select count(*) into n from public.market_listings
     where (seller_id = p_id and created_at > now() - interval '1 day')
        or (buyer_id = p_id and closed_at > now() - interval '1 day' and status = 'sold');
    if n >= k.max_daily then raise exception 'Đã đạt giới hạn % giao dịch trong 24 giờ.', k.max_daily; end if;
  end $$;

  -- ---------- Treo bán ----------
  -- Quyền được cấp thủ công ở Supabase, không tin cờ admin do client gửi.
  create or replace function public.market_listing_unlimited(p_id uuid, p_secret text)
  returns boolean language plpgsql security definer set search_path = public as $$
  begin
    perform public.market_auth(p_id, p_secret, null);
    return (select unlimited_listings from public.market_players where player_id = p_id);
  end $$;
  revoke all on function public.market_listing_unlimited(uuid, text) from public;
  grant execute on function public.market_listing_unlimited(uuid, text) to anon, authenticated;

  drop function if exists public.market_list(uuid, text, text, int, text, int, bigint);
  create or replace function public.market_list(p_id uuid, p_secret text, p_name text, p_realm int,
    p_item text, p_qty int, p_price bigint, p_unlimited boolean default false)
  returns bigint language plpgsql security definer set search_path = public as $$
  declare k record; n int; new_id bigint; unlimited boolean;
  begin
    perform public.market_auth(p_id, p_secret, p_name);
    select p_unlimited and unlimited_listings into unlimited from public.market_players where player_id = p_id;
    if not coalesce(unlimited, false) then
      perform public.market_check_limits(p_id, p_realm);
    end if;
    select * into k from public.market_rules();
    if p_realm < k.min_realm then raise exception 'Cần đạt Trúc Cơ mới giao dịch ở Chợ.'; end if;
    if p_item is null or p_item = '' or p_item in ('linh_thach') then raise exception 'Vật phẩm này không bán được ở Chợ.'; end if;
    if p_qty not between 1 and 9999 then raise exception 'Số lượng không hợp lệ.'; end if;
    if p_price not between 1 and 2000000000 then raise exception 'Giá không hợp lệ.'; end if;
    select count(*) into n from public.market_listings where seller_id = p_id and status = 'open' and expires_at > now();
    if not coalesce(unlimited, false) and n >= k.max_open then raise exception 'Tối đa % món đang treo bán.', k.max_open; end if;
    insert into public.market_listings (seller_id, seller_name, item_id, qty, price, expires_at)
    values (p_id, left(coalesce(p_name, ''), 24), p_item, p_qty, p_price, now() + make_interval(days => k.listing_days))
    returning id into new_id;
    return new_id;
  end $$;

  -- ---------- Mua (nguyên tử: chỉ một người mua được) ----------
  create or replace function public.market_buy(p_id uuid, p_secret text, p_name text, p_realm int, p_listing bigint)
  returns table(item_id text, qty int, price bigint, seller_name text)
  language plpgsql security definer set search_path = public as $$
  declare k record;
  begin
    perform public.market_auth(p_id, p_secret, p_name);
    perform public.market_check_limits(p_id, p_realm);
    select * into k from public.market_rules();
    return query
    update public.market_listings l set
      status = 'sold', buyer_id = p_id, buyer_name = left(coalesce(p_name, ''), 24), closed_at = now(),
      payout = l.price - ceil(l.price * k.tax_pct / 100)::bigint
    where l.id = p_listing and l.status = 'open' and l.expires_at > now() and l.seller_id <> p_id
      and not exists (select 1 from public.market_players s where s.player_id = l.seller_id and s.banned)
      and not exists (select 1 from public.leaderboard lb where lb.player_id = l.seller_id and lb.flagged)
    returning l.item_id, l.qty, l.price, l.seller_name;
    if not found then raise exception 'Món này đã có người mua, đã hủy hoặc hết hạn.'; end if;
  end $$;

  -- ---------- Hủy tin (đồ trả về qua market_claim) ----------
  create or replace function public.market_cancel(p_id uuid, p_secret text, p_listing bigint)
  returns boolean language plpgsql security definer set search_path = public as $$
  begin
    perform public.market_auth(p_id, p_secret, null);
    update public.market_listings set status = 'cancelled', closed_at = now()
     where id = p_listing and seller_id = p_id and status = 'open';
    if not found then raise exception 'Tin này đã bán hoặc không còn treo.'; end if;
    return true;
  end $$;

  -- ---------- Nhận linh thạch đã bán / đồ bị hủy, hết hạn (mỗi tin chỉ nhận một lần) ----------
  create or replace function public.market_claim(p_id uuid, p_secret text)
  returns table(id bigint, status text, item_id text, qty int, payout bigint, buyer_name text)
  language plpgsql security definer set search_path = public as $$
  begin
    perform public.market_auth(p_id, p_secret, null);
    update public.market_listings l set status = 'expired', closed_at = now()
     where l.seller_id = p_id and l.status = 'open' and l.expires_at <= now();
    return query
    update public.market_listings l set seller_claimed = true
     where l.seller_id = p_id and l.status <> 'open' and not l.seller_claimed
    returning l.id, l.status, l.item_id, l.qty, l.payout, l.buyer_name;
  end $$;

  -- ---------- Tin của tôi: đang treo + lịch sử mua/bán 30 ngày ----------
  create or replace function public.market_mine(p_id uuid, p_secret text)
  returns table(id bigint, role text, item_id text, qty int, price bigint, payout bigint, status text,
                other_name text, created_at timestamptz, expires_at timestamptz, closed_at timestamptz)
  language plpgsql security definer set search_path = public as $$
  begin
    perform public.market_auth(p_id, p_secret, null);
    return query
    select l.id, case when l.seller_id = p_id then 'sell' else 'buy' end, l.item_id, l.qty, l.price, l.payout,
           case when l.status = 'open' and l.expires_at <= now() then 'expired' else l.status end,
           case when l.seller_id = p_id then l.buyer_name else l.seller_name end,
           l.created_at, l.expires_at, l.closed_at
    from public.market_listings l
    where (l.seller_id = p_id and (l.status = 'open' or l.closed_at > now() - interval '30 days'))
       or (l.buyer_id = p_id and l.closed_at > now() - interval '30 days')
    order by (l.status = 'open') desc, coalesce(l.closed_at, l.created_at) desc
    limit 60;
  end $$;

  -- ---------- Quyền ----------
  revoke all on function public.market_auth(uuid, text, text) from public, anon, authenticated;
  revoke all on function public.market_check_limits(uuid, int) from public, anon, authenticated;
  revoke all on function public.market_list(uuid, text, text, int, text, int, bigint, boolean) from public;
  revoke all on function public.market_buy(uuid, text, text, int, bigint) from public;
  revoke all on function public.market_cancel(uuid, text, bigint) from public;
  revoke all on function public.market_claim(uuid, text) from public;
  revoke all on function public.market_mine(uuid, text) from public;
  grant execute on function public.market_rules() to anon, authenticated;
  grant execute on function public.market_list(uuid, text, text, int, text, int, bigint, boolean) to anon, authenticated;
  grant execute on function public.market_buy(uuid, text, text, int, bigint) to anon, authenticated;
  grant execute on function public.market_cancel(uuid, text, bigint) to anon, authenticated;
  grant execute on function public.market_claim(uuid, text) to anon, authenticated;
  grant execute on function public.market_mine(uuid, text) to anon, authenticated;

-- Single-buyer migration (also versioned under supabase/migrations).
-- Apply after docs/market/supabase.sql. Safe to run again.
-- No data deletion; existing sold/cancelled/expired listings stay closed.
begin;

create or replace function public.market_listing_guard()
returns trigger language plpgsql set search_path = public as $$
begin
  if row(new.id, new.seller_id, new.seller_name, new.item_id, new.qty, new.price, new.created_at, new.expires_at)
     is distinct from row(old.id, old.seller_id, old.seller_name, old.item_id, old.qty, old.price, old.created_at, old.expires_at) then
    raise exception 'Không được thay đổi nội dung tin đã ký gửi.';
  end if;
  if old.status <> 'open' and
     row(new.status, new.buyer_id, new.buyer_name, new.payout, new.closed_at)
     is distinct from row(old.status, old.buyer_id, old.buyer_name, old.payout, old.closed_at) then
    raise exception 'Tin đã đóng, không thể bán lại hoặc đổi người mua.';
  end if;
  if old.seller_claimed and not new.seller_claimed then
    raise exception 'Giao dịch này đã nhận thưởng.';
  end if;
  if new.status = 'sold' and (new.buyer_id is null or new.buyer_id = new.seller_id or new.closed_at is null) then
    raise exception 'Thông tin người mua không hợp lệ.';
  end if;
  return new;
end $$;
revoke all on function public.market_listing_guard() from public, anon, authenticated;
drop trigger if exists market_listing_single_buyer on public.market_listings;
create trigger market_listing_single_buyer before update on public.market_listings
for each row execute function public.market_listing_guard();

create or replace function public.market_buy_v2(p_id uuid, p_secret text, p_name text, p_realm int, p_listing bigint)
returns table(listing_id bigint, buyer_id uuid, item_id text, qty int, price bigint, seller_name text)
language plpgsql security definer set search_path = public as $$
declare k record; sale public.market_listings;
begin
  perform public.market_auth(p_id, p_secret, p_name);
  perform public.market_check_limits(p_id, p_realm);
  select * into k from public.market_rules();
  -- Lock by immutable primary key; another buyer must wait and see the committed state.
  select l.* into sale from public.market_listings l where l.id = p_listing for update;
  if not found then raise exception 'Món này đã có người mua, đã hủy hoặc hết hạn.'; end if;
  if sale.status <> 'open' or sale.buyer_id is not null or sale.expires_at <= clock_timestamp() or sale.seller_id = p_id then
    raise exception 'Món này đã có người mua, đã hủy hoặc hết hạn.';
  end if;
  if exists (select 1 from public.market_players s where s.player_id = sale.seller_id and s.banned)
     or exists (select 1 from public.leaderboard lb where lb.player_id = sale.seller_id and lb.flagged) then
    raise exception 'Món này đã có người mua, đã hủy hoặc hết hạn.';
  end if;
  update public.market_listings l set status = 'sold', buyer_id = p_id,
    buyer_name = left(coalesce(p_name, ''), 24), closed_at = clock_timestamp(),
    payout = l.price - ceil(l.price * k.tax_pct / 100)::bigint
    where l.id = p_listing and l.status = 'open' and l.buyer_id is null
    returning l.* into sale;
  if not found then raise exception 'Món này đã có người mua, đã hủy hoặc hết hạn.'; end if;
  return query select sale.id, sale.buyer_id, sale.item_id, sale.qty, sale.price, sale.seller_name;
end $$;
revoke all on function public.market_buy_v2(uuid, text, text, int, bigint) from public;
grant execute on function public.market_buy_v2(uuid, text, text, int, bigint) to anon, authenticated;

-- Older game versions retain the same signature and use the same locked purchase path.
create or replace function public.market_buy(p_id uuid, p_secret text, p_name text, p_realm int, p_listing bigint)
returns table(item_id text, qty int, price bigint, seller_name text)
language plpgsql security definer set search_path = public as $$
begin
  return query select r.item_id, r.qty, r.price, r.seller_name
    from public.market_buy_v2(p_id, p_secret, p_name, p_realm, p_listing) r;
end $$;
revoke all on function public.market_buy(uuid, text, text, int, bigint) from public;
grant execute on function public.market_buy(uuid, text, text, int, bigint) to anon, authenticated;

notify pgrst, 'reload schema';
commit;
