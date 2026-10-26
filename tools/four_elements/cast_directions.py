"""Assign authored cast paths; update metadata without touching generated art."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GROUPS = {
    "left": ["tho_linh_dan", "thach_dan", "loi_quang_cau", "loi_bo", "bang_linh_cham", "phong_linh_tien", "phong_nhan", "phong_bo"],
    "above": ["son_nhac", "hau_tho", "loi_kich", "thien_loi", "cuu_tieu", "bang_tien", "huyen_bang", "cuu_thien_phong"],
    "below": ["tho_tuong", "hu_khong_tran_nhac", "dong_dia", "hu_khong_bang_nguc", "cuong_phong", "thien_phong"],
    "center": ["tho_giap", "hon_nguyen_tho", "lien_loi", "hu_khong_kiep_loi", "hon_nguyen_loi", "bang_thuan", "tuyet_doi", "hon_nguyen_bang", "hu_khong_phong_nhan", "hon_nguyen_phong"],
}
ROTATE = {"loi_kich", "cuu_tieu", "bang_tien", "huyen_bang"}

if __name__ == "__main__":
    lookup = {key: direction for direction, skills in GROUPS.items() for key in skills}
    assert len(lookup) == 32
    catalog_path = Path(__file__).parent / "catalog.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    manifest_path = ROOT / "wwwroot/js/four-elements-fx-data.js"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8").split(" = ", 1)[1].strip().rstrip(";"))
    for entries in [catalog, manifest]:
        assert {s["skillId"] for s in entries} == set(lookup)
        for s in entries:
            s["castDirection"] = lookup[s["skillId"]]
            s["rotateWithTravel"] = s["skillId"] in ROTATE
    catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding="utf-8")
    manifest_path.write_text("/* Generated from tools/four_elements/catalog.json by pack.py. */\nconst FOUR_ELEMENTS_FX_DATA = " + json.dumps(manifest, ensure_ascii=False, indent=2) + ";\n", encoding="utf-8")
    (ROOT / "docs/four-elements-art-prompts.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Configured 32 cast paths: " + ", ".join(f"{key}={len(value)}" for key, value in GROUPS.items()))
