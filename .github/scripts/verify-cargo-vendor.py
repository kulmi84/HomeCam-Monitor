"""Check vendored crate bytes and registry coverage against a Cargo.lock.

This verifies integrity/availability, not licensing or reproducibility of a binary.
Uses Python 3.11+ standard library only. No downloaded code is executed.
"""
import argparse
import concurrent.futures
import hashlib
import json
from pathlib import Path
import tomllib


def inspect_crate(checksum_file):
    root = checksum_file.parent.resolve()
    sums = json.loads(checksum_file.read_text(encoding="utf-8"))
    package = tomllib.loads((root / "Cargo.toml").read_text(encoding="utf-8"))["package"]
    errors = []
    for name, expected in sums["files"].items():
        path = (root / name).resolve()
        if not path.is_relative_to(root):
            errors.append(name + ": outside crate")
        elif not path.is_file():
            errors.append(name + ": missing")
        else:
            with path.open("rb") as source:
                actual = hashlib.file_digest(source, "sha256").hexdigest()
            if actual != expected:
                errors.append(name + ": checksum mismatch")
    return {"name": package["name"], "version": package["version"],
            "packageChecksum": sums.get("package"), "files": len(sums["files"]),
            "license": package.get("license"), "errors": errors}


def verify(lock, vendor):
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        crates = list(pool.map(inspect_crate, sorted(vendor.glob("*/.cargo-checksum.json"))))
    lookup = {(x["name"], x["version"]): x for x in crates}
    if len(lookup) != len(crates):
        raise ValueError("Duplicate vendored name/version")
    missing = []
    unsupported = []
    for package in tomllib.loads(lock.read_text(encoding="utf-8"))["package"]:
        source = package.get("source", "")
        if source.startswith("registry+"):
            crate = lookup.get((package["name"], package["version"]))
            if not crate or crate["packageChecksum"] != package["checksum"]:
                missing.append(package["name"] + " " + package["version"])
        elif source:
            # Git dependencies need a separate revision/source mapping.
            unsupported.append(package["name"] + " " + source)
    return {"packages": len(crates), "files": sum(x["files"] for x in crates),
            "missing": missing, "unsupportedSources": unsupported,
            "corrupt": [x for x in crates if x["errors"]], "dependencies": crates}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lock", type=Path, required=True)
    parser.add_argument("--vendor", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = verify(args.lock, args.vendor)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "dependencies"}))
    raise SystemExit(1 if result["missing"] or result["unsupportedSources"] or result["corrupt"] else 0)
