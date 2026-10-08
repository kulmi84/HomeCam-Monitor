"""Archive the actual build inputs; never infer dependency revisions from latest."""
import hashlib
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

root, output = (Path(arg).resolve() for arg in sys.argv[1:3])
output.mkdir(parents=True, exist_ok=True)
source = output / 'corresponding-source'
source.mkdir(exist_ok=True)
records = []
inputs = [p for p in root.iterdir() if p.name not in ('.git', 'build', 'rustup')]
# Cargo sources, when used, are corresponding source; compiler binary caches
# are not. Preserve all downloaded source crates and git checkouts.
for name in ('.cargo/registry', '.cargo/git', '.cargo/config'):
    candidate = root / 'rustup' / name
    if candidate.exists():
        inputs.append(candidate)
if not (root / 'src_packages/mpv').is_dir() or not (root / 'src_packages/ffmpeg').is_dir():
    raise RuntimeError('Primary media sources missing')

# Build recipes can replace submodules with links to separately checked-out
# dependencies. Keep their targets, but make absolute runner paths relocatable.
for link in (root / 'src_packages').rglob('*'):
    if link.is_symlink() and os.path.isabs(os.readlink(link)):
        target = link.resolve(strict=True)
        target.relative_to(root)  # Refuse sources outside the archived build tree.
        relative = os.path.relpath(target, link.parent)
        link.unlink()
        link.symlink_to(relative, target_is_directory=target.is_dir())

for git_dir in (root / 'src_packages').rglob('.git'):
    repo = git_dir.parent
    revision = subprocess.check_output(['git', '-C', str(repo), 'rev-parse', 'HEAD'], text=True).strip()
    remote = subprocess.check_output(['git', '-C', str(repo), 'remote', 'get-url', 'origin'], text=True).strip()
    records.append({'path': str(repo.relative_to(root)), 'revision': revision, 'upstream': remote})
    diff = subprocess.check_output(['git', '-C', str(repo), 'diff', '--binary', '--ignore-submodules=all', 'HEAD'])
    if diff:
        patch = source / (str(repo.relative_to(root)).replace('/', '_') + '.patch')
        patch.write_bytes(diff)

licenses = output / 'licenses'
licenses.mkdir(exist_ok=True)
license_records = []
for item in (root / 'src_packages').rglob('*'):
    if not item.is_file() or '.git' in item.parts:
        continue
    if item.name.upper().startswith(('LICENSE', 'COPYING', 'COPYRIGHT', 'NOTICE', 'AUTHORS')):
        destination = licenses / item.relative_to(root / 'src_packages')
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(item, destination)
        license_records.append(str(destination.relative_to(output)))

build_revision = subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
build_patch = subprocess.check_output(['git', '-C', str(root), 'diff', '--binary', '--ignore-submodules=all', 'HEAD'])
(source / 'build-system.patch').write_bytes(build_patch)
inventory = {
    'buildSystemRevision': build_revision,
    'dependencies': records,
    'licenseFiles': license_records,
    'licenseCompatibilityVerified': False,
    'note': 'Actual sources are archived. Binary license compatibility and Windows behavior still require review.'
}
(output / 'source-inventory.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
shutil.copyfile(__file__, source / 'collect-media-sources.py')
workflow = Path(__file__).resolve().parents[1] / 'workflows/media-sources-build.yml'
shutil.copyfile(workflow, source / 'media-sources-build.yml')
for name in ('CMakeLists.txt', 'README.md'):
    shutil.copyfile(root / name, source / name)
shutil.copytree(root / '.github/workflows', source / 'upstream-workflows', dirs_exist_ok=True)
config = root / 'build/CMakeCache.txt'
shutil.copyfile(config, source / 'CMakeCache.txt')
for filename in ('meson_cross.txt',):
    path = root / 'build' / filename
    if path.exists():
        shutil.copyfile(path, source / filename)

# The archive contains the checked-out source trees, submodules, source tarballs,
# patches and build recipes. Preserve any generated source files as well.
archive = output / 'HomeCam-Media-corresponding-source.tar.zst'
subprocess.run(['tar', '--zstd', '--exclude=.git', '-cf', str(archive),
                '-C', str(root), *[str(p.relative_to(root)) for p in inputs],
                '-C', str(output), 'corresponding-source'], check=True)
for filename in ('mpv.exe', 'ffmpeg.exe'):
    candidates = [p for p in (root / 'build').rglob(filename) if p.is_file()]
    if not candidates:
        raise RuntimeError(f'{filename} missing after build')
    # Prefer installed FFmpeg and the built mpv executable.
    preferred = [p for p in candidates if '/install/' in str(p)] or candidates
    shutil.copyfile(preferred[0], output / filename)

with (output / 'SHA256SUMS.txt').open('w', encoding='ascii') as manifest:
    for item in sorted(output.iterdir()):
        if item.is_file() and item.name != 'SHA256SUMS.txt':
            with item.open('rb') as stream:
                digest = hashlib.file_digest(stream, 'sha256').hexdigest()
            manifest.write(f'{digest}  {item.name}\n')
