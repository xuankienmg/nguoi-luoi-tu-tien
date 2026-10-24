-- Vấn Đạo · Lôi đài (PK giữa người chơi) + danh hiệu Đệ nhất trên Supabase.
-- Cần chạy trước: docs/version/supabase.sql, docs/leaderboard/supabase.sql (dùng bảng leaderboard + view leaderboard_public).
-- Chạy một lần trong Supabase → SQL Editor. Chạy lại an toàn.
--
-- Mô hình bậc thang: mỗi nhân vật giữ một vị trí (pos). Khiêu chiến người xếp trên trong tầm;
-- thắng thì hai người đổi vị trí cho nhau, thua thì giữ nguyên. Đánh người xếp dưới là tỷ thí: không đổi hạng, không tính thắng / thua.
-- Trận có đủ kỹ năng nên game tính (Game/PvpCombat.cs), Supabase giữ luật và thứ tự:
--   arena_begin  kiểm tra lượt / tầm, cấp seed ngẫu nhiên, trả ảnh chụp kỹ năng của đối thủ (arena_ranks.loadout);
--   arena_finish nhận kết quả của đúng trận vừa cấp (một lần, trong 3 phút) rồi đổi hạng.
-- Trận bắt đầu mà không báo kết quả vẫn tính một lượt và tính là thua.
-- Anon key chỉ đọc được view arena_public / titles_public và gọi các hàm arena_state / arena_begin / arena_finish / arena_set_loadout;
-- mọi hàm đều kiểm tra secret.

-- ---------- Luật Lôi đài (sửa ở đây rồi chạy lại file; nhớ sửa cả ArenaService) ----------
-- daily: lượt khiêu chiến mỗi ngày (làm mới 0h giờ Việt Nam) · cooldown_sec: nghỉ giữa hai trận
-- min_window / window_pct: tầm khiêu chiến = max(min_window, window_pct% hạng hiện tại) hạng phía trên
-- finish_sec: thời hạn báo kết quả sau arena_begin
drop function if exists public.arena_rules();
create or replace function public.arena_rules()
returns table(daily int, cooldown_sec int, min_window int, window_pct int, finish_sec int)
language sql immutable as $$ select 15, 20, 5, 20, 180 $$;

create table if not exists public.arena_ranks (
  player_id uuid primary key references public.leaderboard(player_id) on delete cascade,
  pos       bigint not null,
  wins      int    not null default 0,
  losses    int    not null default 0,
  joined_at timestamptz not null default now(),
  -- kiểm tra cuối câu lệnh để hai người đổi pos trong một UPDATE
  constraint arena_pos_uniq unique (pos) deferrable initially immediate
);
-- ảnh chụp kỹ năng / hiệu ứng / trang phục mang vào trận (PvpFighter), game tự cập nhật
alter table public.arena_ranks add column if not exists loadout jsonb;
alter table public.arena_ranks add column if not exists loadout_at timestamptz;

create table if not exists public.arena_fights (
  id            bigint generated always as identity primary key,
  attacker_id   uuid    not null,
  defender_id   uuid    not null,
  attacker_name text    not null,
  defender_name text    not null,
  win           boolean not null,          -- bên khiêu chiến thắng
  attacker_rank bigint  not null,          -- hạng hiển thị trước trận
  defender_rank bigint  not null,
  swapped       boolean not null default false,
  created_at    timestamptz not null default now()
);
alter table public.arena_fights add column if not exists seed   bigint;
alter table public.arena_fights add column if not exists status text not null default 'done';   -- pending (đã cấp seed) · done
alter table public.arena_fights add column if not exists practice boolean not null default false;   -- tỷ thí với người xếp dưới: không đổi hạng, không tính thắng / thua
create index if not exists arena_fights_attacker_idx on public.arena_fights (attacker_id, created_at desc);
create index if not exists arena_fights_defender_idx on public.arena_fights (defender_id, created_at desc);

alter table public.arena_ranks  enable row level security;
alter table public.arena_fights enable row level security;
revoke all on public.arena_ranks, public.arena_fights from anon, authenticated;

-- Hạng hiển thị: thứ tự pos, bỏ nhân vật bị gắn cờ dữ liệu (họ không vào được Lôi đài)
create or replace view public.arena_public as
select a.player_id, l.name, l.realm, l.stage, l.realm_name, l.stage_name, l.power, a.wins, a.losses,
       row_number() over (order by a.pos) as rank
from public.arena_ranks a
join public.leaderboard l on l.player_id = a.player_id
where not l.flagged;
grant select on public.arena_public to anon, authenticated;

-- Danh hiệu: người hạng 1 của từng bảng (Cảnh giới, Lực chiến, Vượt ải, Linh thú) và Lôi đài
create or replace view public.titles_public as
select 'realm'::text as category, player_id, name from public.leaderboard_public where realm_rank = 1 and not flagged
union all
select 'power', player_id, name from public.leaderboard_public where power_rank = 1 and not flagged
union all
select 'progress', player_id, name from public.leaderboard_public where progress_rank = 1 and progress > 0 and not flagged
union all
select 'pet', player_id, name from public.leaderboard_public where pet_rank = 1 and pet_power > 0 and not flagged
union all
select 'arena', player_id, name from public.arena_public where rank = 1;
grant select on public.titles_public to anon, authenticated;

-- ---------- Nội bộ ----------
create or replace function public.arena_auth(p_id uuid, p_secret text)
returns public.leaderboard language plpgsql security definer set search_path = public as $$
declare r public.leaderboard;
begin
  perform public.require_version();      -- docs/version/supabase.sql: chặn bản game cũ
  select * into r from public.leaderboard where player_id = p_id;
  if not found then raise exception 'Nhân vật chưa có trên bảng xếp hạng. Chờ vài giây rồi thử lại.'; end if;
  if r.secret_hash <> encode(sha256(convert_to(coalesce(p_secret, ''), 'UTF8')), 'hex') then raise exception 'Sai khóa nhân vật.'; end if;
  if r.flagged then raise exception 'Dữ liệu nhân vật từng được mở bằng bản game cũ nên không vào được Lôi đài.'; end if;
  return r;
end $$;

-- Vào Lôi đài lần đầu: đứng cuối bậc thang
create or replace function public.arena_join(p_id uuid)
returns void language plpgsql security definer set search_path = public as $$
begin
  if exists (select 1 from public.arena_ranks where player_id = p_id) then return; end if;
  lock table public.arena_ranks in share row exclusive mode;     -- hai người cùng vào không lấy trùng pos
  insert into public.arena_ranks (player_id, pos)
  select p_id, coalesce(max(pos), 0) + 1 from public.arena_ranks
  on conflict (player_id) do nothing;
end $$;

create or replace function public.arena_window(my_rank bigint)
returns bigint language sql immutable set search_path = public as $$
  select greatest(k.min_window, ceil(my_rank * k.window_pct / 100.0))::bigint from public.arena_rules() k
$$;

-- 0h hôm nay theo giờ Việt Nam
create or replace function public.arena_day_start()
returns timestamptz language sql stable as $$
  select date_trunc('day', now() at time zone 'Asia/Ho_Chi_Minh') at time zone 'Asia/Ho_Chi_Minh'
$$;

create or replace function public.arena_check_loadout(p_loadout jsonb)
returns void language plpgsql immutable as $$
begin
  if p_loadout is not null and (jsonb_typeof(p_loadout) <> 'object' or pg_column_size(p_loadout) > 4000) then
    raise exception 'Ảnh chụp kỹ năng không hợp lệ.';
  end if;
end $$;

-- bản cũ: trận tính bằng SQL (chỉ số, không kỹ năng)
drop function if exists public.arena_fight(uuid, text, uuid);
drop function if exists public.arena_duel(jsonb, jsonb, bigint, int);
drop function if exists public.arena_stat(jsonb, text, numeric);

-- ---------- Trạng thái Lôi đài (tự vào bậc thang nếu chưa có) ----------
create or replace function public.arena_state(p_id uuid, p_secret text)
returns jsonb language plpgsql security definer set search_path = public as $$
declare k record; my_rank bigint; w bigint; used int; last_at timestamptz;
begin
  perform public.arena_auth(p_id, p_secret);
  perform public.arena_join(p_id);
  select * into k from public.arena_rules();
  select rank into my_rank from public.arena_public where player_id = p_id;
  w := public.arena_window(my_rank);
  select count(*) filter (where created_at >= public.arena_day_start()), max(created_at)
    into used, last_at from public.arena_fights where attacker_id = p_id;
  return jsonb_build_object(
    'me', (select to_jsonb(a) from public.arena_public a where a.player_id = p_id),
    'top', (select coalesce(jsonb_agg(to_jsonb(a) order by a.rank), '[]'::jsonb) from public.arena_public a where a.rank <= 3),
    -- danh sách chọn đánh (trừ top 3 đã hiện riêng): bản thân, mọi người trong tầm phía trên, và 7 người có hạng gần mình nhất (trên hoặc dưới)
    'targets', (select coalesce(jsonb_agg(to_jsonb(a) order by a.rank), '[]'::jsonb) from public.arena_public a
                where a.rank > 3 and (a.player_id = p_id or a.rank between my_rank - w and my_rank - 1
                  or a.player_id in (select n.player_id from public.arena_public n where n.rank > 3 and n.player_id <> p_id
                                     order by abs(n.rank - my_rank), n.rank limit 7))),
    'window', w, 'used', used, 'daily', k.daily,
    'ready_at', case when last_at + make_interval(secs => k.cooldown_sec) > now() then last_at + make_interval(secs => k.cooldown_sec) end,
    'total', (select count(*) from public.arena_public),
    -- trận đang chờ kết quả (chưa hết hạn) chưa hiện
    'history', (select coalesce(jsonb_agg(h order by h.id desc), '[]'::jsonb) from (
        select f.id, f.attacker_id = p_id as attacker,
               case when f.attacker_id = p_id then f.defender_name else f.attacker_name end as other_name,
               case when f.attacker_id = p_id then f.win else not f.win end as win,
               case when f.attacker_id = p_id then f.attacker_rank else f.defender_rank end as from_rank,
               case when not f.swapped then case when f.attacker_id = p_id then f.attacker_rank else f.defender_rank end
                    when f.attacker_id = p_id then f.defender_rank else f.attacker_rank end as to_rank,
               f.practice, f.created_at
        from public.arena_fights f
        where (f.attacker_id = p_id or f.defender_id = p_id)
          and (f.status = 'done' or f.created_at < now() - make_interval(secs => k.finish_sec))
        order by f.id desc limit 20) h));
end $$;

-- ---------- Cập nhật ảnh chụp kỹ năng (game gọi nền khi trang bị / kỹ năng đổi) ----------
create or replace function public.arena_set_loadout(p_id uuid, p_secret text, p_loadout jsonb)
returns boolean language plpgsql security definer set search_path = public as $$
declare n int;
begin
  perform public.arena_auth(p_id, p_secret);
  perform public.arena_check_loadout(p_loadout);
  update public.arena_ranks set loadout = p_loadout, loadout_at = now() where player_id = p_id;
  get diagnostics n = row_count;
  return n > 0;
end $$;

-- ---------- Khiêu chiến: bắt đầu (cấp seed + ảnh chụp đối thủ) ----------
create or replace function public.arena_begin(p_id uuid, p_secret text, p_target uuid, p_loadout jsonb)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  me public.leaderboard; foe public.leaderboard; k record; foe_loadout jsonb; practice boolean;
  used int; last_at timestamptz; my_rank bigint; foe_rank bigint; seed bigint; fight_id bigint;
begin
  me := public.arena_auth(p_id, p_secret);
  if p_target = p_id then raise exception 'Không thể tự khiêu chiến chính mình.'; end if;
  perform public.arena_check_loadout(p_loadout);
  perform public.arena_join(p_id);
  select * into k from public.arena_rules();
  update public.arena_ranks set loadout = coalesce(p_loadout, loadout), loadout_at = now() where player_id = p_id;
  -- khóa dòng của mình: hai lần bấm cùng lúc không vượt giới hạn lượt
  perform 1 from public.arena_ranks where player_id = p_id for update;
  select count(*) filter (where created_at >= public.arena_day_start()), max(created_at)
    into used, last_at from public.arena_fights where attacker_id = p_id;
  if used >= k.daily then raise exception 'Đã dùng hết % lượt khiêu chiến hôm nay. Làm mới lúc 0h.', k.daily; end if;
  if last_at > now() - make_interval(secs => k.cooldown_sec) then
    raise exception 'Đang điều tức, chờ % giây nữa.', ceil(extract(epoch from last_at + make_interval(secs => k.cooldown_sec) - now()));
  end if;
  select rank into my_rank from public.arena_public where player_id = p_id;
  select rank into foe_rank from public.arena_public where player_id = p_target;
  if foe_rank is null then raise exception 'Đối thủ không còn ở Lôi đài.'; end if;
  -- người xếp dưới: tỷ thí (không đổi hạng); người xếp trên: phải trong tầm
  practice := foe_rank > my_rank;
  if not practice and my_rank - foe_rank > public.arena_window(my_rank) then
    raise exception 'Đối thủ ngoài tầm: chỉ khiêu chiến được trong % hạng phía trên.', public.arena_window(my_rank);
  end if;
  select * into foe from public.leaderboard where player_id = p_target;
  select loadout into foe_loadout from public.arena_ranks where player_id = p_target;
  seed := floor(random() * 2147483647)::bigint;
  insert into public.arena_fights (attacker_id, defender_id, attacker_name, defender_name, win, attacker_rank, defender_rank, swapped, seed, status, practice)
  values (p_id, p_target, me.name, foe.name, false, my_rank, foe_rank, false, seed, 'pending', practice)
  returning id into fight_id;
  return jsonb_build_object('id', fight_id, 'seed', seed, 'foe_name', foe.name, 'foe_realm', foe.realm,
    'foe_loadout', foe_loadout, 'foe_stats', foe.stats, 'old_rank', my_rank, 'foe_rank', foe_rank, 'practice', practice);
end $$;

-- ---------- Khiêu chiến: báo kết quả (một lần, trong thời hạn) rồi đổi hạng ----------
create or replace function public.arena_finish(p_id uuid, p_secret text, p_fight bigint, p_win boolean)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  k record; f public.arena_fights; my_rank bigint; foe_rank bigint; pa bigint; pd bigint; swap boolean;
begin
  perform public.arena_auth(p_id, p_secret);
  select * into k from public.arena_rules();
  select * into f from public.arena_fights
   where id = p_fight and attacker_id = p_id and status = 'pending' and created_at > now() - make_interval(secs => k.finish_sec)
   for update;
  if not found then raise exception 'Trận này đã kết thúc hoặc hết hạn.'; end if;
  -- khóa hai vị trí theo thứ tự cố định (tránh deadlock)
  perform 1 from public.arena_ranks where player_id in (p_id, f.defender_id) order by player_id for update;
  select rank into my_rank from public.arena_public where player_id = p_id;
  select rank into foe_rank from public.arena_public where player_id = f.defender_id;
  -- đối thủ đã tụt xuống dưới mình (hoặc rời Lôi đài) trong lúc đánh: thắng vẫn ghi nhưng không đổi hạng
  -- tỷ thí (lúc bắt đầu đối thủ xếp dưới): chỉ ghi lịch sử, không đổi hạng, không tính thắng / thua
  swap := not f.practice and coalesce(p_win, false) and foe_rank is not null and my_rank is not null and foe_rank < my_rank;
  if swap then
    select pos into pa from public.arena_ranks where player_id = p_id;
    select pos into pd from public.arena_ranks where player_id = f.defender_id;
    update public.arena_ranks set pos = case when player_id = p_id then pd else pa end where player_id in (p_id, f.defender_id);
  end if;
  if not f.practice then
    update public.arena_ranks set
      wins   = wins   + case when (player_id = p_id) = coalesce(p_win, false) then 1 else 0 end,
      losses = losses + case when (player_id = p_id) = coalesce(p_win, false) then 0 else 1 end
    where player_id in (p_id, f.defender_id);
  end if;
  update public.arena_fights set status = 'done', win = coalesce(p_win, false), swapped = swap,
    attacker_rank = coalesce(my_rank, attacker_rank), defender_rank = coalesce(foe_rank, defender_rank)
  where id = f.id;
  return jsonb_build_object('old_rank', my_rank, 'new_rank', case when swap then foe_rank else my_rank end, 'practice', f.practice);
end $$;

revoke all on function public.arena_auth(uuid, text) from public, anon, authenticated;
revoke all on function public.arena_join(uuid) from public, anon, authenticated;
revoke all on function public.arena_state(uuid, text) from public;
revoke all on function public.arena_set_loadout(uuid, text, jsonb) from public;
revoke all on function public.arena_begin(uuid, text, uuid, jsonb) from public;
revoke all on function public.arena_finish(uuid, text, bigint, boolean) from public;
grant execute on function public.arena_state(uuid, text) to anon, authenticated;
grant execute on function public.arena_set_loadout(uuid, text, jsonb) to anon, authenticated;
grant execute on function public.arena_begin(uuid, text, uuid, jsonb) to anon, authenticated;
grant execute on function public.arena_finish(uuid, text, bigint, boolean) to anon, authenticated;
