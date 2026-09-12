"""Validate a P01 session's real observations, commands, and reset boundaries."""
import argparse
import json
import math
from pathlib import Path


def validate(path, minimum_resets):
    previous = None
    resets = 0
    commands = set()
    count = 0
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        row = json.loads(line)
        assert row["schema"] == 1, f"Line {number}: unknown schema"
        for field in ("player_position", "robot_position", "robot_velocity", "desired_velocity", "target", "player_input"):
            assert all(math.isfinite(v) for v in row[field].values()), f"Line {number}: nonfinite {field}"
        if previous:
            assert row["sequence"] == previous["sequence"] + 1, f"Line {number}: sequence gap"
            if row["episode"] == previous["episode"]:
                assert row["time"] >= previous["time"] and row["tick"] >= previous["tick"], f"Line {number}: clock moved backwards"
            else:
                assert row["episode"] == previous["episode"] + 1 and row["type"] == "reset", f"Line {number}: invalid episode boundary"
                assert previous["type"] == "episode_end", f"Line {number}: missing previous episode end"
        if row["type"] == "reset":
            resets += 1
            assert row["command_id"] == 0 and row["mode"] == "Wait" and row["status"] == "waiting"
            assert not row["has_path"] and row["tick"] == 0
            assert all(abs(v) < 0.001 for v in row["robot_velocity"].values())
            assert all(v == 0 for v in row["player_input"].values())
        if row["type"].startswith("command_"):
            commands.add(row["type"])
        previous = row
        count += 1
    assert resets >= minimum_resets, f"Only {resets} resets; expected {minimum_resets}"
    return {"passed": True, "records": count, "resets_including_start": resets, "commands": sorted(commands)}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("log", type=Path)
    parser.add_argument("--minimum-resets", type=int, default=1)
    args = parser.parse_args()
    print(json.dumps(validate(args.log, args.minimum_resets), ensure_ascii=False, indent=2))
