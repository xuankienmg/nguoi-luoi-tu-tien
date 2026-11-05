-- Chợ: tin rao định giá bằng linh thạch hoặc Cực phẩm linh thạch (item thuong_pham_linh_thach).
-- Apply after docs/market/supabase.sql and 20261024_market_single_buyer.sql. Safe to run again. No data deletion.
-- Bản game mới gọi market_list (thêm p_currency), market_buy_v3, market_claim_v2, market_mine.
-- Bản game cũ (market_buy_v2 / market_buy / market_claim) không mua hay nhận tiền của tin định giá bằng Cực phẩm,
-- để không trả / nhận nhầm linh thạch thường.
begin;

alter table public.market_listings add column if not exists currency text not null default 'linh_thach';
alter table public.market_listings drop constraint if exists market_listings_currency_check;
alter table public.market_listings add constraint market_listings_currency_check
  check (currency in ('linh_thach', 'thuong_pham_linh_thach'));

-- Nội dung tin (gồm tiền tệ) không đổi sau khi ký gửi
create or replace function public.market_listing_guard()
returns trigger language plpgsql set search_path = public as $$
begin
  if row(new.id, new.seller_id, new.seller_name, new.item_id, new.qty, new.price, new.currency, new.created_at, new.expires_at)
     is distinct from row(old.id, old.seller_id, old.seller_name, old.item_id, old.qty, old.price, old.currency, old.created_at, old.expires_at) then
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

-- Cột mới thêm vào cuối view (bản cũ chọn cột theo tên nên không ảnh hưởng)
create or replace view public.market_open as
select l.id, l.seller_id, l.seller_name, l.item_id, l.qty, l.price, l.created_at, l.expires_at, l.currency
from public.market_listings l
join public.market_players p on p.player_id = l.seller_id and not p.banned
where l.status = 'open' and l.expires_at > now()
  and not exists (select 1 from public.leaderboard lb where lb.player_id = l.seller_id and lb.flagged);
grant select on public.market_open to anon, authenticated;

-- ---------- Treo bán: thêm p_currency (mặc định linh thạch, bản cũ gọi như trước) ----------
drop function if exists public.market_list(uuid, text, text, int, text, int, bigint, boolean);
create or replace function public.market_list(p_id uuid, p_secret text, p_name text, p_realm int,
  p_item text, p_qty int, p_price bigint, p_unlimited boolean default false, p_currency text default 'linh_thach')
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
  if p_item is null or p_item = '' or p_item in ('linh_thach', 'thuong_pham_linh_thach') then raise exception 'Vật phẩm này không bán được ở Chợ.'; end if;
  if coalesce(p_currency, '') not in ('linh_thach', 'thuong_pham_linh_thach') then raise exception 'Loại tiền tệ không hợp lệ.'; end if;
  if p_qty not between 1 and 9999 then raise exception 'Số lượng không hợp lệ.'; end if;
  if p_price not between 1 and 2000000000 then raise exception 'Giá không hợp lệ.'; end if;
  select count(*) into n from public.market_listings where seller_id = p_id and status = 'open' and expires_at > now();
  if not coalesce(unlimited, false) and n >= k.max_open then raise exception 'Tối đa % món đang treo bán.', k.max_open; end if;
  insert into public.market_listings (seller_id, seller_name, item_id, qty, price, currency, expires_at)
  values (p_id, left(coalesce(p_name, ''), 24), p_item, p_qty, p_price, p_currency, now() + make_interval(days => k.listing_days))
  returning id into new_id;
  return new_id;
end $$;
revoke all on function public.market_list(uuid, text, text, int, text, int, bigint, boolean, text) from public;
grant execute on function public.market_list(uuid, text, text, int, text, int, bigint, boolean, text) to anon, authenticated;

-- ---------- Mua v3: trả thêm currency ----------
create or replace function public.market_buy_v3(p_id uuid, p_secret text, p_name text, p_realm int, p_listing bigint)
returns table(listing_id bigint, buyer_id uuid, item_id text, qty int, price bigint, seller_name text, currency text)
language plpgsql security definer set search_path = public as $$
declare k record; sale public.market_listings;
begin
  perform public.market_auth(p_id, p_secret, p_name);
  perform public.market_check_limits(p_id, p_realm);
  select * into k from public.market_rules();
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
  return query select sale.id, sale.buyer_id, sale.item_id, sale.qty, sale.price, sale.seller_name, sale.currency;
end $$;
revoke all on function public.market_buy_v3(uuid, text, text, int, bigint) from public;
grant execute on function public.market_buy_v3(uuid, text, text, int, bigint) to anon, authenticated;

-- Bản cũ: chỉ mua được tin định giá bằng linh thạch (không biết trừ Cực phẩm)
create or replace function public.market_buy_v2(p_id uuid, p_secret text, p_name text, p_realm int, p_listing bigint)
returns table(listing_id bigint, buyer_id uuid, item_id text, qty int, price bigint, seller_name text)
language plpgsql security definer set search_path = public as $$
begin
  if exists (select 1 from public.market_listings l where l.id = p_listing and l.currency <> 'linh_thach') then
    raise exception 'Tin này định giá bằng Cực phẩm linh thạch. Hãy cập nhật game để mua.';
  end if;
  return query select r.listing_id, r.buyer_id, r.item_id, r.qty, r.price, r.seller_name
    from public.market_buy_v3(p_id, p_secret, p_name, p_realm, p_listing) r;
end $$;
revoke all on function public.market_buy_v2(uuid, text, text, int, bigint) from public;
grant execute on function public.market_buy_v2(uuid, text, text, int, bigint) to anon, authenticated;

-- ---------- Nhận tiền bán / đồ trả về v2: trả thêm currency ----------
create or replace function public.market_claim_v2(p_id uuid, p_secret text)
returns table(id bigint, status text, item_id text, qty int, payout bigint, buyer_name text, currency text)
language plpgsql security definer set search_path = public as $$
begin
  perform public.market_auth(p_id, p_secret, null);
  update public.market_listings l set status = 'expired', closed_at = now()
   where l.seller_id = p_id and l.status = 'open' and l.expires_at <= now();
  return query
  update public.market_listings l set seller_claimed = true
   where l.seller_id = p_id and l.status <> 'open' and not l.seller_claimed
  returning l.id, l.status, l.item_id, l.qty, l.payout, l.buyer_name, l.currency;
end $$;
revoke all on function public.market_claim_v2(uuid, text) from public;
grant execute on function public.market_claim_v2(uuid, text) to anon, authenticated;

-- Bản cũ: tiền bán bằng Cực phẩm để lại chờ bản mới nhận (đồ trả về vẫn nhận bình thường)
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
     and not (l.status = 'sold' and l.currency <> 'linh_thach')
  returning l.id, l.status, l.item_id, l.qty, l.payout, l.buyer_name;
end $$;
revoke all on function public.market_claim(uuid, text) from public;
grant execute on function public.market_claim(uuid, text) to anon, authenticated;

-- ---------- Tin của tôi: thêm currency (đổi kiểu trả về nên phải drop) ----------
drop function if exists public.market_mine(uuid, text);
create or replace function public.market_mine(p_id uuid, p_secret text)
returns table(id bigint, role text, item_id text, qty int, price bigint, payout bigint, status text,
              other_name text, created_at timestamptz, expires_at timestamptz, closed_at timestamptz, currency text)
language plpgsql security definer set search_path = public as $$
begin
  perform public.market_auth(p_id, p_secret, null);
  return query
  select l.id, case when l.seller_id = p_id then 'sell' else 'buy' end, l.item_id, l.qty, l.price, l.payout,
         case when l.status = 'open' and l.expires_at <= now() then 'expired' else l.status end,
         case when l.seller_id = p_id then l.buyer_name else l.seller_name end,
         l.created_at, l.expires_at, l.closed_at, l.currency
  from public.market_listings l
  where (l.seller_id = p_id and (l.status = 'open' or l.closed_at > now() - interval '30 days'))
     or (l.buyer_id = p_id and l.closed_at > now() - interval '30 days')
  order by (l.status = 'open') desc, coalesce(l.closed_at, l.created_at) desc
  limit 60;
end $$;
revoke all on function public.market_mine(uuid, text) from public;
grant execute on function public.market_mine(uuid, text) to anon, authenticated;

notify pgrst, 'reload schema';
commit;
