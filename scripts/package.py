"""Package a previously built and tested Windows executable; never include saves."""
from pathlib import Path
import hashlib
import zipfile

root = Path(__file__).resolve().parent.parent
output = root / 'releases' / 'Sovereign-0.3-windows.zip'
files = {
    'Sovereign.exe': root / 'bin/Release/net472/Sovereign.exe',
    'Sovereign.exe.config': root / 'bin/Release/net472/Sovereign.exe.config',
    'НАЧАТЬ ЗДЕСЬ.md': root / 'docs/PLAYER-GUIDE.md',
    'ИСТОЧНИКИ.txt': root / 'docs/archive-0.2/ИСТОЧНИКИ.txt',
    'ПРОВЕРКИ-СИМУЛЯЦИИ.txt': root / 'docs/validation/tests-sdk.txt',
    'ПРОВЕРКИ-ИНТЕРФЕЙСА.txt': root / 'docs/validation/ui-results.txt',
}
for source in files.values():
    if not source.is_file():
        raise SystemExit(f'Missing build or validation file: {source}')
for name in ('ПРОВЕРКИ-СИМУЛЯЦИИ.txt', 'ПРОВЕРКИ-ИНТЕРФЕЙСА.txt'):
    report = files[name].read_text(encoding='utf-8-sig')
    if 'Failures: 0' not in report or '\nFAIL ' in report or 'PASS ' not in report:
        raise SystemExit(f'Validation failed: {name}')
output.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for name, source in files.items():
        archive.write(source, 'Sovereign-0.3/' + name)
with zipfile.ZipFile(output) as archive:
    if archive.testzip() is not None:
        raise SystemExit('ZIP integrity check failed')
checksum = hashlib.sha256(output.read_bytes()).hexdigest()
output.with_suffix('.zip.sha256').write_text(f'{checksum}  {output.name}\n')
print(f'Packaged {output.name}: {output.stat().st_size} bytes, SHA-256 {checksum}')
