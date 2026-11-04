---
name: can-bang-chi-so
description: Cân bằng / đổi chỉ số trong game (tinh thạch, trang bị, đan dược, quái, kỹ năng, linh thú, giá, tỉ lệ rơi). Dùng khi người dùng muốn tăng/giảm, buff/nerf, chia lại chỉ số, đổi công thức chỉ số, hoặc hỏi "chỉ số thay đổi thế nào" (vd "tinh thạch tấn công không có máu", "giảm giá đan", "xem chỉ số thay đổi theo từng viên").
---

# Cân bằng chỉ số (Vấn Đạo)

Quy trình: **phương án → duyệt → sửa → so sánh trước/sau từ code thật → test → ghi chú phát hành**. Chỉ số nằm ở backend C# (`Game/`); tìm đúng file theo bản đồ trong `CLAUDE.md`.

## 1. Đề xuất phương án trước, chưa sửa code

1. Grep nơi sinh chỉ số (vd tinh thạch: `GemProgression.Create`, `GemRatingBudget`; cộng dồn: `GemData.GetSocketedBonuses`; áp vào trận: `GameService.BattleAt`, `PvpCombat.Run`, `GameService.PvpFighterOf`).
2. Gửi người dùng:
   - **Hiện trạng**: bảng ngắn, nêu rõ chỗ nào vi phạm yêu cầu.
   - **Phương án**: các bước, các file sẽ sửa. Nếu cần chỉ số **mới**, kiểm tra nó đã có chưa và nó áp vào đòn nào. Ví dụ `linh_ap`/`SkillPct` nhân vào **mọi đòn**, còn `gem_ultimate_damage` chỉ nhân vào tuyệt kỹ.
   - **Số mẫu** ở cấp 1 / cấp 10 (hoặc mốc tương đương). Tính đúng công thức, ví dụ cấp 10 dùng `1,45^9` chứ không phải `1,45^10`.
   - **Ảnh hưởng**: chỉ rõ những gì **giảm**. Lưu ý chỉ số % theo cảnh giới tăng dần theo cảnh giới, còn chỉ số cố định thì không.
3. Yêu cầu mơ hồ thì hỏi bằng lựa chọn cụ thể (AskUserQuestion, có preview), không tự đoán. Chờ người dùng duyệt rồi mới sửa.

## 2. Sửa

- Chỉ đổi giá trị/công thức, kèm chú thích ngắn `// cân bằng MM/YYYY: ...`. Không refactor lan rộng.
- Giữ nguyên ID vật phẩm. Các viên cũ đi qua `GemProgression.RebalanceLegacy` → `Create`, nên tự nhận chỉ số mới.
- Chỉ số tính từ định nghĩa thì không cần migration. Chỉ số **đã lưu** trong DB/ảnh chụp Lôi đài thì phải cân nhắc tương thích: thêm trường mới vào cuối record, có giá trị mặc định (vd `GemBonuses`).
- Thêm hiển thị nếu người chơi cần thấy, vd bảng hiệu ứng trong `GameService` (`Uncapped(...)`/`Limit(...)`).

## 3. Bảng so sánh trước/sau từ code thật

Không tính tay. Xuất số liệu từ cả code gốc lẫn code đã sửa:

```bash
S="<scratchpad>"
git worktree add -q --detach "$S/old" master     # code gốc, tách khỏi thư mục đang làm
cp <mẫu dump> test/GemSystemTests/StatDumpTmp.cs
cp <mẫu dump> "$S/old/test/GemSystemTests/"
(cd test/GemSystemTests && STAT_DUMP="$S/new.json" dotnet test -p:BaseOutputPath="$S/build/" --filter "FullyQualifiedName~StatDumpTmp")
(cd "$S/old/test/GemSystemTests" && STAT_DUMP="$S/old.json" dotnet test --filter "FullyQualifiedName~StatDumpTmp")
rm test/GemSystemTests/StatDumpTmp.cs; git worktree remove --force "$S/old"
```

Mẫu dump: tạo file `StatDumpTmp.cs` trong scratchpad, **không commit**. Đổi nguồn `GemData.Items` sang nguồn của hệ đang sửa:

```csharp
using System.Text.Json; using VanDao.Game; using Xunit;
namespace GemSystemTests;
public class StatDumpTmp
{
    [Fact]
    public void Dump()
    {
        var path = Environment.GetEnvironmentVariable("STAT_DUMP");
        if (string.IsNullOrEmpty(path)) return;
        var rows = GemData.Items.Where(i => i.Type == "gem").Select(i => new {
            i.Id, i.Name, i.GemCategory, i.GemLevel, Atk = (double)i.Atk, Def = (double)i.Def, Hp = (double)i.Hp, i.CritPct,
            Fx = (i.Effects ?? []).GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.Sum(e => e.Value)) });
        File.WriteAllText(path, JsonSerializer.Serialize(rows));
    }
}
```

- Dùng `node` để ghép `old.json` với `new.json` theo `Id` (máy không có Python). Chỉ giữ cột có thay đổi.
- Trình bày bằng widget (`show_widget`): mỗi hệ hoặc nhóm một nút, mỗi cấp một hàng, ô ghi "cũ → mới". Tô xanh khi tăng, đỏ khi giảm, xám khi giữ nguyên; tô riêng cột trọng tâm nếu có.
- Trong câu trả lời, liệt kê ngắn những gì **giảm**, và vật phẩm cũ nào chịu ảnh hưởng theo.

## 4. Test

- Chạy test liên quan trước: `dotnet test test/GemSystemTests --filter "FullyQualifiedName~<Tên>"`. Cuối cùng chạy cả bộ một lần.
- **Game đang mở** (lỗi `MSB3027 ... NguoiLuoiTuTien.exe is locked`): không tắt game của người dùng. Thêm `-p:BaseOutputPath="<scratchpad>/build/"` để build ra thư mục khác.
- Test cũ sai vì số thay đổi đúng thiết kế: sửa số kỳ vọng và nói rõ lý do. Test kiểm tra một cơ chế đã bị bỏ (vd % nền cảnh giới): viết lại theo cơ chế mới, đừng xóa im lặng.
- Thêm test bất biến cho quy tắc mới (vd mọi viên tấn công có `Def == 0 && Hp == 0`; cùng nhóm thì cùng tổng chỉ số).
- Test lỗi chập chờn: chạy riêng test đó để xác nhận, rồi báo rõ cho người dùng. Không nói "tất cả đều qua" nếu có lần chạy lỗi.
- Công cụ `tools/combat_rating_checks` chạy **không có** tinh thạch và linh thú, nên không đo được thay đổi của hai hệ này.

## 5. Bàn giao

- Thêm mục vào `docs/releases/upcoming.md` theo skill `release-notes` (bảng "cũ → mới" khi có từ 3 dòng số liệu trở lên). Làm việc này khi người dùng đồng ý hoặc khi sắp commit.
- Tóm tắt: cái gì tăng, cái gì giảm, test đã chạy, điều gì chưa kiểm tra trong game. Game đang mở vẫn chạy bản cũ, phải mở lại mới thấy thay đổi.
- Không commit hay push khi chưa được yêu cầu. Đang ở `master` thì tạo nhánh trước.
