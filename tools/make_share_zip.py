"""Create and inspect a clean, relocatable Windows x64 sharing package."""
from pathlib import Path
import shutil
import struct
import zipfile
import sys
import re

root = Path(__file__).resolve().parent.parent
source = root / "release" / "时砂便携版"
archive = root / "release" / "时砂-Windows-x64-便携版.zip"
check_root = root / ".package-check" / "朋友 接收"
if len(sys.argv) > 1:
    version = sys.argv[1]
    assert re.fullmatch(r"v\d+\.\d+\.\d+", version), "Use a version such as v1.1.0"
    source = root / "release" / version / "时砂便携版"
    archive = root / "release" / f"Chronova-{version}-windows-x64-portable.zip"
    check_root = root / ".package-check" / version / "朋友 接收"
assert source.is_dir() and not archive.exists(), "Package source missing or ZIP already exists"
assert not check_root.exists(), "Verification destination already exists"
files = sorted(p for p in source.rglob("*") if p.is_file())
assert not any(p.is_symlink() for p in source.rglob("*")), "Unexpected package link"
assert not any("data" in p.relative_to(source).parts for p in files), "Personal data must not be included"
assert (source / "read.md").is_file(), "read.md is required"
exe = (source / "app" / "时砂.exe").read_bytes()
header = struct.unpack_from("<I", exe, 0x3C)[0]
assert exe[header:header + 4] == b"PE\0\0" and struct.unpack_from("<H", exe, header + 4)[0] == 0x8664, "Executable is not Windows x64"

with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED, compresslevel=6) as output:
    for path in files:
        output.write(path, str(Path(source.name) / path.relative_to(source)))
with zipfile.ZipFile(archive) as packaged:
    assert packaged.testzip() is None, "ZIP CRC check failed"
    names = packaged.namelist()
    assert len(names) == len(files)
    assert not any("/data/" in name.replace("\\", "/") for name in names)
    assert all(not Path(name).is_absolute() and ".." not in Path(name).parts for name in names)
    check_root.mkdir(parents=True)
    packaged.extractall(check_root)
for path in files:
    restored = check_root / source.name / path.relative_to(source)
    assert restored.read_bytes() == path.read_bytes(), f"Extracted content mismatch: {path.name}"
shutil.copy2(source / "read.md", root / "release" / "read.md")
print(f"ZIP: {archive}")
print(f"SIZE: {archive.stat().st_size / 1024 / 1024:.2f} MiB; FILES: {len(files)}")
print("PASS ZIP CRC, every extracted file, x64 architecture, Unicode paths and no personal data")
print(f"EXTRACTED_APP: {check_root / source.name / 'app'}")
