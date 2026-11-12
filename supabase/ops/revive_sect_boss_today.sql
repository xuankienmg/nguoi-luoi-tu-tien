-- HỒI SINH Boss tông hôm nay (giờ VN) để đánh lại với máu mới. Chạy tay trên Supabase SQL Editor, KHÔNG phải migration.
-- Xóa dòng Boss + lượt đánh của hôm nay: lần mở Bệ Phong Ấn đầu tiên Boss được tạo lại (máu tính lại theo SectBossData.HpShare hiện hành),
-- mỗi người có lại 2 lượt, rương hôm nay chưa nhận. Buff tu vi ngày mai do hạ Boss bị bỏ.
-- Cống hiến đã nhận từ các lượt trước giữ nguyên (nằm ở SectMembers / Sects.Fund). Tin nhắn "đã bị hạ" trong chat tông vẫn còn.
-- Đổi v_sect_id thành mã tông để chỉ hồi sinh một tông; để NULL là hồi sinh mọi tông.

DO $$
DECLARE
    v_sect_id bigint := NULL;
    v_day text := to_char((now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 'YYYY-MM-DD');
    v_next text := to_char(((now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date + 1), 'YYYY-MM-DD');
    n_hits int; n_boss int; n_buff int;
BEGIN
    DELETE FROM game."SectBossHits" WHERE "Day" = v_day AND (v_sect_id IS NULL OR "SectId" = v_sect_id);
    GET DIAGNOSTICS n_hits = ROW_COUNT;
    DELETE FROM game."SectBosses" WHERE "Day" = v_day AND (v_sect_id IS NULL OR "SectId" = v_sect_id);
    GET DIAGNOSTICS n_boss = ROW_COUNT;
    UPDATE game."Sects" SET "BuffDay" = '' WHERE "BuffDay" = v_next AND (v_sect_id IS NULL OR "Id" = v_sect_id);
    GET DIAGNOSTICS n_buff = ROW_COUNT;
    UPDATE game."SectMembers" SET "BossRef" = 0 WHERE v_sect_id IS NULL OR "SectId" = v_sect_id;   -- mức tham chiếu máu Boss: cũ lưu theo sát thương, nay theo công
    RAISE NOTICE 'Ngày %: xóa % dòng lượt đánh, % dòng Boss, bỏ buff % tông', v_day, n_hits, n_boss, n_buff;
END $$;
