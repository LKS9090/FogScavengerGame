"""Check P02 acceptance telemetry (standard library only)."""
import json
import math
import sys
from collections import Counter
from pathlib import Path


def validate(path):
    rows = [json.loads(line) for line in Path(path).read_text(encoding="utf-8").splitlines()]
    assert rows, "empty log"
    counts = Counter()
    last_episode = 0
    last_time = 0
    for i, row in enumerate(rows):
        assert row["sequence"] == i, "sequence discontinuity"
        assert row["environment"] == "p02-greybox-v1"
        assert row["episode"] >= last_episode
        assert math.isfinite(row["time"]) and row["time"] >= 0
        if row["episode"] == last_episode:
            assert row["time"] >= last_time, "time reversed"
        for field in ("player", "robot", "goal", "velocity", "input"):
            assert all(math.isfinite(v) for v in row[field].values()), field
        if row["type"] == "reset":
            assert row["mode"] == "Follow" and not row["running"]
            assert not any(row[k] for k in ("collected", "completed", "door_open", "arrival_prompt"))
            assert all(v == 0 for v in row["input"].values())
        counts[row["type"]] += 1
        last_episode, last_time = row["episode"], row["time"]
    assert counts["reset"] >= 11
    assert counts["item_collected"] == 1 and counts["task_complete"] == 1
    assert counts["gate_open"] >= 1 and counts["gate_closed"] >= 1
    return {"passed": True, "records": len(rows), "events": dict(counts)}


if __name__ == "__main__":
    print(json.dumps(validate(sys.argv[1]), ensure_ascii=False, indent=2))
