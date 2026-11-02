using VanDao.Data;

namespace VanDao.Game;

/// <summary>Ảnh chụp một nhân vật cho bảng xếp hạng: Supabase (submit_score) và backend online (LeaderboardEntries) dùng chung.
/// Key gom mọi thứ hiện trên bảng, đổi Key nghĩa là cần gửi / ghi lại.</summary>
public sealed record LeaderboardSnapshot(string Name, int Realm, int Stage, int Level, decimal Qi, string RealmName, string StageName,
    decimal Power, long Progress, decimal PetPower, Dictionary<string, string> Equipment, Dictionary<string, double> Stats, bool Flagged, string Key)
{
    public static LeaderboardSnapshot Of(Player p, bool dev)
    {
        var r = GameService.Realm(p);
        var power = GameService.Power(p);
        var progress = GameService.LeaderboardProgress(p);
        var petPower = GameService.LeaderboardPetPower(p);
        var bestPet = GameService.LeaderboardBestPet(p);
        var skin = GameService.ShownSkin(p);
        var equipment = p.Equipment.Where(e => e.ItemId.Length > 0).OrderBy(e => e.Slot).ToDictionary(e => e.Slot, e => e.ItemId);
        var st = GameService.Stats(p);
        var key = $"{p.Name}|{p.Realm}|{p.Stage}|{power}|{progress}|{petPower}|{bestPet?.DefId}|{bestPet?.Level}|{bestPet?.StarPts}|{bestPet?.AppearanceForm}|{skin?.Id}|{string.Join(',', equipment.Values)}";
        var stats = new Dictionary<string, double> { ["progress"] = progress, ["pet_power"] = (double)petPower, ["atk"] = (double)st.Atk, ["def"] = (double)st.Def, ["hp"] = (double)st.Hp, ["crit"] = st.Crit, ["dodge"] = st.Dodge, ["accuracy"] = st.Accuracy,
            ["crit_resist"] = st.CritResist, ["combo"] = st.Combo, ["combo_resist"] = st.ComboResist, ["reflect"] = st.Reflect, ["reflect_resist"] = st.ReflectResist };
        if (skin is not null) stats["skin:" + skin.Id] = 1;   // trang phục đang hiện → ảnh nhân vật trên bảng (LeaderboardService.RankedHero)
        if (bestPet is not null)
        {
            stats["pet_id:" + bestPet.DefId] = bestPet.Level;
            stats["pet_stars"] = PetData.StarsOf(bestPet);
            stats["pet_form"] = GameService.PetAppearance(bestPet);
        }
        return new(p.Name, p.Realm, p.Stage, GameService.Level(p), Math.Round(p.Qi, 1), r.Name, r.Stages[p.Stage],
            power, progress, petPower, equipment, stats, SaveGuard.DataFlagged(p, dev), key);
    }
}
