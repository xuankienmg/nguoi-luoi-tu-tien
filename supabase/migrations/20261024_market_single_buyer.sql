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
