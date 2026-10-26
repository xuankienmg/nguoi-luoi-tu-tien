using VanDao.Game;
using VanDao.Data;
using System.Text.Json;
/// <summary>Linh thú Thượng Cổ mang hai hệ (cố định theo loài); cộng hưởng ngũ hành cộng dồn cả hai hệ.</summary>
internal static class DualElementChecks
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string msg){if(!ok)throw new Exception(msg);checks++;}
        foreach(var def in PetData.Definitions){
            var ancient=def.Rarity=="ancient";
            Check(def.Elements.Length==(ancient?2:1),def.Id+" element count");
            Check(def.Elements[0]==def.Element,def.Id+" primary element first");
            if(ancient)Check(def.Elements[1]!=def.Element&&SkillData.Elements.Any(e=>e.Id==def.Elements[1]),def.Id+" second element valid and distinct");
            // save cũ: con Thượng Cổ đã có (chỉ lưu DefId) nhận đủ hai hệ khi dựng DTO
            var p=new Player();p.Pets.Add(new Pet{DefId=def.Id});
            var dto=GameService.PetStateFor(p).Owned.Single();
            Check(dto.Elements!.SequenceEqual(def.Elements)&&dto.Element==def.Element,def.Id+" DTO elements");
            Check(dto.Training!.ResonanceParts!.Select(x=>x.Element).SequenceEqual(def.Elements),def.Id+" resonance parts per element");
        }
        Check(PetData.ById["that_thai_ho_diep"].Elements.SequenceEqual(new[]{"moc","thuy"}),"Butterfly is Wood + Water");
        Check(PetData.ById["thai_co_loi_long"].Elements.SequenceEqual(new[]{"loi","gio"}),"Thunder dragon is Lightning + Wind");
        // cộng dồn: Mộc + Thủy với công pháp Thủy = cùng hệ (12%) + tương sinh (5%)
        var (pct,kind,parts)=PetTraining.Resonance(new[]{"moc","thuy"},"thuy");
        Check(Math.Abs(pct-(PetTraining.ResonanceSame+PetTraining.ResonanceSupport))<1e-9&&kind=="same"&&parts.Length==2,"Two elements stack same + support");
        (pct,kind,_)=PetTraining.Resonance(new[]{"loi","gio"},"hoa");
        Check(Math.Abs(pct-PetTraining.ResonanceSupport)<1e-9&&kind=="support","Only Wind supports Fire");
        (pct,kind,_)=PetTraining.Resonance(new[]{"loi"},"loi");
        Check(Math.Abs(pct-PetTraining.ResonanceSame)<1e-9&&kind=="same","Single element unchanged");
        (pct,kind,_)=PetTraining.Resonance(new[]{"moc","thuy"},"");
        Check(pct==0&&kind=="","No active manual, no resonance");
        // giao diện / wiki đọc "Elements" từ PetDef đã tuần tự hóa
        Check(JsonSerializer.Serialize(PetData.ById["thai_co_loi_long"]).Contains("\"Elements\":[\"loi\",\"gio\"]"),"PetDef serializes Elements");
        Console.WriteLine($"PASS {checks} dual element checks.");
    }
}
