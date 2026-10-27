"""Check that every UI string has all eight non-default translations."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
texts = set()
for filename in ("MainWindow.xaml.cs", "MainWindow.Tools.cs", "MainWindow.Weather.cs", "MainWindow.China.cs", "App.xaml.cs"):
    source = (root / filename).read_text(encoding="utf-8-sig")
    for chinese, english in re.findall(r'T\("((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)"\)', source):
        texts.add(json.loads('"' + english + '"'))
    for chinese, english in re.findall(r'Tip\(\w+,\s*"([^"]*)",\s*"([^"]*)"\)', source):
        texts.add(english)
    for chinese, english in re.findall(r'UiText\.Get\([^,]+,\s*"((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)"\)', source):
        texts.add(json.loads('"' + english + '"'))
source = (root / "MainWindow.xaml").read_text(encoding="utf-8-sig")
texts.update(re.findall(r'Tag="[^"|]*\|([^"]*)"', source))
source = (root / "Weather.cs").read_text(encoding="utf-8-sig")
texts.update(re.findall(r'new\("[A-Z]{2}", "[^"]*", "([^"]*)"\)', source))
texts.update(re.findall(r'=> \("[^"]*", "([^"]*)", ', source))
catalog = root / "Localization.json"
if not catalog.exists():
    print("\n".join(sorted(texts)))
    print("COUNT", len(texts))
else:
    data = json.loads(catalog.read_text(encoding="utf-8-sig"))
    missing = texts - data.keys()
    for key, values in data.items():
        assert len(values) == 8 and all(isinstance(v, str) and v.strip() for v in values), key
    assert not missing, f"Missing translations: {sorted(missing)}"
    print(f"PASS {len(texts)} UI strings have translations for all 10 languages")
