"""Exercise the real Unity ML-Agents wire protocol. Does not train or save a model."""
import argparse
import json
from pathlib import Path

import numpy as np
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel


def main(executable, report_path):
    engine = EngineConfigurationChannel()
    engine.set_configuration_parameters(time_scale=20, quality_level=0, target_frame_rate=-1)
    parameters = EnvironmentParametersChannel()
    parameters.set_float_parameter("configuration", 0)
    env = UnityEnvironment(file_name=str(Path(executable).resolve()), seed=42, no_graphics=True,
                           side_channels=[engine, parameters], timeout_wait=60, worker_id=7)
    checks = []
    try:
        env.reset()
        name = next(iter(env.behavior_specs))
        spec = env.behavior_specs[name]
        assert name.startswith("SwitchMotorV1")
        assert [s.shape for s in spec.observation_specs] == [(28,)]
        assert spec.action_spec.continuous_size == 2 and spec.action_spec.discrete_size == 0
        checks.append("Real Unity handshake exposes 28 observations and 2 continuous actions")
        for index in range(180):
            decision, terminal = env.get_steps(name)
            if len(terminal):
                assert terminal.interrupted[0] and terminal.reward[0] == 0
                assert terminal.obs[0][0][9] >= .999
                break
            assert len(decision) == 1 and np.isfinite(decision.obs[0]).all()
            env.set_actions(name, ActionTuple(continuous=np.zeros((1, 2), dtype=np.float32)))
            env.step()
        else:
            raise AssertionError("No timeout terminal received")
        checks.append("Zero actions produce a genuine time-limit truncation, not success")
        parameters.set_float_parameter("configuration", 0)
        env.reset()
        for index in range(180):
            decision, terminal = env.get_steps(name)
            if len(terminal):
                assert not terminal.interrupted[0] and terminal.reward[0] == 1
                assert terminal.obs[0][0][7] == 1 and terminal.obs[0][0][8] >= .999
                break
            obs = decision.obs[0][0]
            delta = obs[[0, 2]] * 12
            distance = np.linalg.norm(delta)
            # A simple test fixture in the unobstructed configuration, not a trained teacher.
            action = delta / max(distance, 1e-6) * min(3.5, distance * 2.5) / 6.5
            if distance < .16:
                action[:] = 0
            env.set_actions(name, ActionTuple(continuous=action.astype(np.float32)[None, :]))
            env.step()
        else:
            raise AssertionError("No successful terminal received")
        checks.append("Python-issued movement really reaches and holds the switch; success terminal reward is 1")
        for configuration in range(10):
            parameters.set_float_parameter("configuration", configuration)
            env.reset()
            decision, terminal = env.get_steps(name)
            assert len(decision) == 1 and len(terminal) == 0
            obs = decision.obs[0][0]
            assert np.isfinite(obs).all() and np.allclose(obs[[3, 4, 5, 6, 7, 8, 10, 11]], 0)
            # R21 can execute one zero-action physics tick before the first post-reset decision.
            assert 0 <= obs[9] <= .02 / 15 + 1e-6, f"Reset timer config {configuration}: {obs[9]}"
        checks.append("Ten Python resets clear motion and hold; first returned observation is at most one 20ms physics tick after reset")
        report = {"passed": True, "behavior": name, "observation_count": 28,
                  "continuous_actions": 2, "trained_model": False, "checks": checks}
        Path(report_path).parent.mkdir(parents=True, exist_ok=True)
        Path(report_path).write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report, indent=2))
    finally:
        env.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("executable")
    parser.add_argument("--report", default="Reports/SwitchTraining/python-interface.json")
    args = parser.parse_args()
    main(args.executable, args.report)
