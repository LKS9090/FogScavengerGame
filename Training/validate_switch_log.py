"""Validate actual environment acceptance records, not training performance."""
import json
import math
import sys
from collections import Counter
from pathlib import Path


def validate(path):
    rows = [json.loads(line) for line in Path(path).read_text(encoding="utf-8").splitlines()]
    assert rows
    events = Counter()
    configurations = set()
    last_episode, last_step = 0, 0
    for sequence, row in enumerate(rows):
        assert row["sequence"] == sequence and row["environment"] == "switch-local-v1"
        assert row["motor"] == "capsule-motor-v1" and row["episode"] >= last_episode
        if row["episode"] == last_episode:
            assert row["physics_step"] >= last_step
        assert abs(row["elapsed"] - .02 * row["physics_step"]) < 1e-4
        obs = row["observation"]
        assert len(obs) == 28 and all(math.isfinite(v) for v in obs)
        assert all(0 <= v <= 1 for v in obs[12:])
        assert sum(v * v for v in row["action"].values()) <= 1.00001
        for field in ("position", "goal", "velocity"):
            assert all(math.isfinite(v) for v in row[field].values())
        if row["type"] == "reset":
            assert row["reset_valid"] and row["physics_step"] == 0 and row["hold"] == 0
            assert row["collision_steps"] == 0 and not row["pressed"]
            assert all(v == 0 for v in obs[3:12])
        if row["type"] == "success":
            assert row["pressed"] and row["hold"] >= 2.999 and row["reward"] == 1
            assert math.hypot(row["velocity"]["x"], row["velocity"]["z"]) <= .15001
            configurations.add(row["configuration"])
        if row["type"] == "timeout":
            assert row["elapsed"] >= 15 and row["reward"] == 0
        events[row["type"]] += 1
        last_episode, last_step = row["episode"], row["physics_step"]
    assert configurations == set(range(20))
    assert events["reset"] >= 10
    assert all(events[k] >= 1 for k in ("timeout", "out_of_bounds", "invalid_action"))
    return {"passed": True, "records": len(rows), "valid_configurations": 20, "events": dict(events)}


if __name__ == "__main__":
    print(json.dumps(validate(sys.argv[1]), indent=2))
