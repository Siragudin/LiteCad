# -*- coding: utf-8 -*-
import copy
import re
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent
EN_PATH = ROOT / "Resources" / "Strings.resx"
RU_PATH = ROOT / "Resources" / "Strings.ru.resx"
PS1_PATH = ROOT / "_gen_ru_resx.ps1"

ENGLISH_ADDS = {
    "Label.DefaultProjectName": "Project",
    "Format.AreaSuffix": " m²",
}


def load_translations() -> dict[str, str]:
    text = PS1_PATH.read_text(encoding="utf-8")
    match = re.search(r"\$raw = @'\r?\n(.*?)'@", text, re.DOTALL)
    if not match:
        raise RuntimeError("Translation block not found in _gen_ru_resx.ps1")
    translations: dict[str, str] = {}
    for line in match.group(1).splitlines():
        line = line.strip()
        if not line or "=" not in line:
            continue
        key, value = line.split("=", 1)
        translations[key] = value
    return translations


def ensure_english_keys(tree: ET.ElementTree) -> None:
    root = tree.getroot()
    existing = {node.attrib["name"] for node in root.findall("data")}
    for key, value in ENGLISH_ADDS.items():
        if key in existing:
            node = next(n for n in root.findall("data") if n.attrib["name"] == key)
            value_el = node.find("value")
            if value_el is not None:
                value_el.text = value
            continue
        data = ET.SubElement(root, "data", {"name": key, "{http://www.w3.org/XML/1998/namespace}space": "preserve"})
        value_el = ET.SubElement(data, "value")
        value_el.text = value


def write_resx(path: Path, tree: ET.ElementTree) -> None:
    xml = ET.tostring(tree.getroot(), encoding="utf-8")
    path.write_bytes(b'<?xml version="1.0" encoding="utf-8"?>\r\n' + xml)


def main() -> None:
    translations = load_translations()
    en_tree = ET.parse(EN_PATH)
    ensure_english_keys(en_tree)
    write_resx(EN_PATH, en_tree)

    keys = [node.attrib["name"] for node in en_tree.getroot().findall("data")]
    missing = [key for key in keys if key not in translations]
    if missing:
        raise RuntimeError(f"Missing translations: {missing}")

    ru_tree = copy.deepcopy(en_tree)
    for node in ru_tree.getroot().findall("data"):
        key = node.attrib["name"]
        value_el = node.find("value")
        if value_el is not None:
            value_el.text = translations[key]

    write_resx(RU_PATH, ru_tree)
    print(f"English keys: {len(keys)}")
    print(f"Russian keys: {len(keys)}")
    print(f"Missing translations: {len(missing)}")


if __name__ == "__main__":
    main()
