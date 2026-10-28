"""Package a previously built and tested Windows executable; never include saves."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parent.parent
output = root / 'releases' / 'Sovereign-0.16-windows.zip'
files = {
    'Sovereign.exe': root / 'bin/Release/net472/Sovereign.exe',
    'OpenTK.dll': root / 'bin/Release/net472/OpenTK.dll',
    'OpenTK.GLControl.dll': root / 'bin/Release/net472/OpenTK.GLControl.dll',
    'OpenTK.dll.config': root / 'bin/Release/net472/OpenTK.dll.config',
    'OpenTK-LICENSE.txt': root / 'lib/OpenTK-LICENSE.txt',
    'docs/RENDERING.md': root / 'docs/RENDERING.md',
    'Sovereign.exe.config': root / 'bin/Release/net472/Sovereign.exe.config',
    'НАЧАТЬ ЗДЕСЬ.md': root / 'docs/PLAYER-GUIDE.md',
    'docs/SCENARIO-DATA.md': root / 'docs/SCENARIO-DATA.md',
    'docs/DOCTRINES.md': root / 'docs/DOCTRINES.md',
    'docs/CHANGES-0.9.md': root / 'docs/CHANGES-0.9.md',
    'docs/CHANGES-0.10.md': root / 'docs/CHANGES-0.10.md',
    'docs/GOVERNMENT.md': root / 'docs/GOVERNMENT.md',
    'docs/CHANGES-0.11.md': root / 'docs/CHANGES-0.11.md',
    'docs/CHANGES-0.12.md': root / 'docs/CHANGES-0.12.md',
    'docs/CHANGES-0.13.md': root / 'docs/CHANGES-0.13.md',
    'docs/TRADE.md': root / 'docs/TRADE.md',
    'docs/OVERHAUL-PLAN.md': root / 'docs/OVERHAUL-PLAN.md',
    'docs/OVERHAUL-BRIEF.md': root / 'docs/OVERHAUL-BRIEF.md',
    'docs/CHANGES-0.14.md': root / 'docs/CHANGES-0.14.md',
    'docs/MILITARY-READINESS.md': root / 'docs/MILITARY-READINESS.md',
    'docs/CHANGES-0.15.md': root / 'docs/CHANGES-0.15.md',
    'docs/CHANGES-0.16.md': root / 'docs/CHANGES-0.16.md',
    'docs/CHANGES-0.5.md': root / 'docs/CHANGES-0.5.md',
    'docs/TECHNOLOGIES.md': root / 'docs/TECHNOLOGIES.md',
    'docs/CHANGES-0.8.md': root / 'docs/CHANGES-0.8.md',
    'docs/CHANGES-0.7.md': root / 'docs/CHANGES-0.7.md',
    'docs/CHANGES-0.6.md': root / 'docs/CHANGES-0.6.md',
    'docs/REQUIREMENTS-STATUS.md': root / 'docs/REQUIREMENTS-STATUS.md',
    'docs/PRODUCT-BRIEF.md': root / 'docs/PRODUCT-BRIEF.md',
    'docs/validation/0.16/README.md': root / 'docs/validation/0.16/README.md',
    'ИСТОЧНИКИ.txt': root / 'docs/archive-0.2/ИСТОЧНИКИ.txt',
    'ПРОВЕРКИ-СИМУЛЯЦИИ.txt': root / 'docs/validation/0.16/tests-sdk.txt',
    'ПРОВЕРКИ-ВИДЕОРЕНДЕРА.txt': root / 'docs/validation/0.16/gpu-results.txt',
    'ПРОВЕРКИ-ИНТЕРФЕЙСА.txt': root / 'docs/validation/0.16/ui-results.txt',
}
for evidence in (root / 'docs/validation/0.16').iterdir():
    if evidence.suffix in ('.txt', '.json', '.png'):
        files[evidence.relative_to(root).as_posix()] = evidence
receipt = json.loads((root / 'docs/validation/0.16/build-receipt.json').read_text())
exe = files['Sovereign.exe']
if hashlib.sha256(exe.read_bytes()).hexdigest() != receipt['executable_sha256']:
    raise SystemExit('Executable differs from the tested build')
for name, checksum in receipt['runtime_sha256'].items():
    if hashlib.sha256(files[name].read_bytes()).hexdigest() != checksum:
        raise SystemExit(f'Runtime dependency differs from tested build: {name}')
source_files = sorted([*root.glob('src/*.cs'), *root.glob('lib/*'), *root.glob('assets/scenario-*.tsv'), root / 'Sovereign.csproj', root / 'assets/world.bin', root / 'assets/app.ico'], key=lambda p: p.relative_to(root).as_posix())
digest = hashlib.sha256()
for source in source_files:
    digest.update(source.relative_to(root).as_posix().encode() + b'\0' + source.read_bytes() + b'\0')
if digest.hexdigest() != receipt['source_sha256']:
    raise SystemExit('Sources changed after validation; rebuild and test before packaging')
for source in files.values():
    if not source.is_file():
        raise SystemExit(f'Missing build or validation file: {source}')
for name in ('ПРОВЕРКИ-СИМУЛЯЦИИ.txt', 'ПРОВЕРКИ-ИНТЕРФЕЙСА.txt', 'ПРОВЕРКИ-ВИДЕОРЕНДЕРА.txt'):
    report = files[name].read_text(encoding='utf-8-sig')
    if 'Failures: 0' not in report or '\nFAIL ' in report or 'PASS ' not in report:
        raise SystemExit(f'Validation failed: {name}')
output.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for name, source in files.items():
        archive.write(source, 'Sovereign-0.16/' + name)
with zipfile.ZipFile(output) as archive:
    if archive.testzip() is not None:
        raise SystemExit('ZIP integrity check failed')
checksum = hashlib.sha256(output.read_bytes()).hexdigest()
output.with_suffix('.zip.sha256').write_text(f'{checksum}  {output.name}\n')
print(f'Packaged {output.name}: {output.stat().st_size} bytes, SHA-256 {checksum}')
