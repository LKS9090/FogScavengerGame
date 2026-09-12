"""Validate paired decision data, whole-episode splits and real terminal outcomes; no training."""
import argparse
import json
import math
from pathlib import Path


def validate(directory):
    directory = Path(directory)
    report = json.loads((directory / "report.json").read_text(encoding="utf-8-sig"))
    assert report["passed"] and report["accepted"] == report["episodes"] == 256
    keys = set()
    counts = {}
    for split in ("train", "validation"):
        groups = {}
        count = 0
        for line in (directory / f"{split}.jsonl").read_text(encoding="utf-8").splitlines():
            item = json.loads(line)
            key = (item["configuration"], item["variant"])
            assert item["split"] == split and 0 <= key[0] < 16
            assert (0 <= key[1] < 12) if split == "train" else (12 <= key[1] < 16)
            assert len(item["observation"]) == len(item["next_observation"]) == 28
            assert len(item["action"]) == 2
            assert all(math.isfinite(v) for field in ("observation", "next_observation", "action") for v in item[field])
            assert sum(v*v for v in item["action"]) <= 1.00001
            for field in ("observation", "next_observation"):
                assert all(0 <= v <= 1 for v in item[field][12:])
            previous = groups.get(key)
            if previous:
                assert not previous["terminal"]
                assert item["decision"] == previous["decision"] + 1
                assert item["observation"] == previous["next_observation"]
            else:
                assert key not in keys and item["decision"] == 0
                assert all(item["observation"][i] == 0 for i in (3,4,5,6,7,8,9,10,11))
                keys.add(key)
            ticks = item["physics_steps"]
            assert 1 <= ticks <= 5
            assert abs((item["next_observation"][9] - item["observation"][9])*15 - ticks*.02) < 1e-5
            assert all(abs(item["next_observation"][5+i] - item["action"][i]) < 1e-6 for i in range(2))
            if item["terminal"]:
                assert item["outcome"] == "success" and item["reward"] == 1 and not item["truncated"]
                end = item["next_observation"]
                assert end[7] == 1 and end[8] >= .9999
                assert math.hypot(end[3],end[4])*6.5 <= .15001
            else:
                assert ticks == 5 and item["reward"] == 0 and item["outcome"] == "running"
            groups[key] = item
            count += 1
        assert all(item["terminal"] for item in groups.values())
        assert len(groups) == (192 if split == "train" else 64)
        assert count == report["training_samples" if split == "train" else "validation_samples"]
        counts[split] = {"episodes": len(groups), "samples": count}
    assert not (directory / "rejected.jsonl").read_text(encoding="utf-8").strip()
    assert all(x["collision_steps"] == 0 and x["accepted"] for x in report["results"])
    result = {"passed": True, "counts": counts, "checks": ["finite 28/2 vectors", "pre-action pairing and temporal continuity",
        "10Hz decisions / 50Hz actual movement", "applied action matches label", "zero state at every reset",
        "whole-episode train/validation split", "reserved configurations 16-19 absent", "success, hold, speed and zero collisions"],
        "trained_model": False}
    (directory / "data-validation.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory")
    validate(parser.parse_args().directory)
