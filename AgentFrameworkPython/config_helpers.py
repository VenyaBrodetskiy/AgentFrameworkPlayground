# Helpers for loading configuration in a C#-like way (optional base + required development override).

import json
from pathlib import Path
from typing import Any


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def require_str(config: dict[str, Any], key: str) -> str:
    value = config.get(key)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{key} not found in appsettings.Development.json")
    return value


def resolve_appsettings_paths(here: Path) -> tuple[Path | None, Path]:
    """Mimic the C# sample: appsettings.json optional, appsettings.Development.json required."""
    solution_root = here.parent

    base = solution_root / "AgentFramework" / "appsettings.json"
    base_path = base if base.exists() else None

    candidates = [
        here / "appsettings.Development.json",
        solution_root / "AgentFramework" / "appsettings.Development.json",
        solution_root / "AgentFramework" / "bin" / "Debug" / "net10.0" / "appsettings.Development.json",
    ]
    dev_path = next((p for p in candidates if p.exists()), None)
    if dev_path is None:
        raise FileNotFoundError(
            "appsettings.Development.json not found. Expected one of:\n" + "\n".join(f"- {p}" for p in candidates)
        )
    return base_path, dev_path

