"""Make an explicit source allowlist and check that a public upload excludes personal data."""
import json
from pathlib import Path
import re
import zipfile

root = Path(__file__).resolve().parent.parent
paths = []
for pattern in ("*.cs", "*.xaml", "*.csproj", "*.json"):
    paths.extend(root.glob(pattern))
paths += [root / "README.md", root / ".gitignore", root / "NuGet.Config", root / "创建桌面快捷方式.ps1"]
paths += list((root / "assets").glob("*")) + list((root / "docs").glob("*.md"))
for folder in (root / "tests", root / "tests" / "ui", root / "tools"):
    paths += [p for p in folder.iterdir() if p.is_file() and p.suffix in (".cs", ".csproj", ".py", ".ps1", ".json")
              and p.name != "publish_github.ps1"]
files = sorted(set(p.relative_to(root).as_posix() for p in paths if p.is_file()))
assert all(not any(part in {"app", "release", "data", "bin", "obj", ".git", ".upload"} for part in Path(p).parts) for p in files)
assert "README.md" in files and "docs/read.md" in files
secret = re.compile(r"gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]+|sk-[A-Za-z0-9]{20,}|-----BEGIN (?:RSA |OPENSSH )?PRIVATE KEY-----")
for name in files:
    if Path(name).suffix not in (".png", ".ico"):
        assert not secret.search((root / name).read_text(encoding="utf-8-sig")), f"Credential-shaped text: {name}"
archive = root / "release" / "时砂-Windows-x64-便携版.zip"
with zipfile.ZipFile(archive) as zipped:
    assert not any("/data/" in p.replace("\\", "/") for p in zipped.namelist())
    assert zipped.testzip() is None
destination = root / ".upload"
destination.mkdir(exist_ok=True)
(destination / "source-files.json").write_text(json.dumps(files, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"PUBLIC SOURCE CHECK: {len(files)} files, {sum((root / p).stat().st_size for p in files) / 1024 / 1024:.2f} MiB")
print("PASS explicit allowlist, no app/data/cache/build output, no credential-shaped text; clean ZIP verified")
