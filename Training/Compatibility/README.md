# ML-Agents Release 21 本机兼容验证

2026-09-11 实测通过。此组合用于官方工具链验证，不代表所有游戏任务或未来模型都已验证。

## 已验证组合

- Unity 2022.3.62f3c1；官方 Release 21 源码提交 7a03145ae48ad354821bd89e0243d99332149ace。
- Unity ML-Agents 3.0.0-exp.1，Sentis 1.2.0-exp.2。完整解析版本见 unity-packages-lock.json。
- Conda 环境 fog-mlagents：Python 3.10.12、PyTorch 2.7.1+cu128、NumPy 1.23.5。
- Python mlagents / mlagents-envs 均为 1.0.0，从本地 Release 21 源码安装；修改了 mlagents-envs 对 NumPy 的固定依赖，因此不是未经修改的官方发布组合。
- grpcio 1.48.2、protobuf 3.19.6、ONNX 1.12.0、TensorBoard 2.14.1、huggingface-hub 0.16.4、gym 0.26.2。
- setuptools 69.5.1、wheel 0.43.0。避免旧源码使用的 pkg_resources 在新构建工具中不可用。
- 原 fog-gpu（Python 3.12）保留，未改动。

## 修复范围

唯一 ML-Agents 源码适配是 ml-agents-envs/setup.py 的 numpy==1.21.2 改为 numpy==1.23.5，补丁附在 release21-numpy.patch。原版本缺少 Windows CPython 3.10 wheel；1.23.5 同时保留训练器使用的 np.float 等旧别名。不使用 --no-deps 掩盖冲突，最终 pip check 通过。

固定旧协议依赖和兼容构建工具；PyTorch 2.7.1 的 checkpoint 恢复和 ONNX 导出实际通过，没有修改 torch.load 或关闭安全加载默认行为。仍有 torch.set_default_tensor_type 弃用警告，不影响此次运行。

## 验证证据

1. 独立新环境 GPU 矩阵计算、反向传播通过；pip check 无依赖冲突；mlagents-learn --help 成功。
2. 官方 3DBall 场景用本机 Unity 构建为独立 Windows 程序成功。
3. 使用 cuda 设备的 PPO 短训练，目标 8192 步，实际最终检查点 8202；成功保存并导出 ONNX。
4. --resume 从 8202 步恢复，目标 16384，实际最终检查点 16511；成功再次导出。批量采样导致步数略超过目标。
5. 最终 ONNX 通过 onnx.checker；输入 obs_0=[batch,8]。
6. Unity Sentis CPU 实际执行最终模型，100 组固定 8 维输入，每组返回 2 个有限的 deterministic_continuous_actions。

训练日志见 train-smoke.log 和 train-resume.log。本次是短流程验证，不是策略性能评估。Sentis 检查是模型执行验证，不等于完整游戏闭环、跨后端数值一致性或性能基准。用户游戏工程没有安装 ML-Agents，官方实验在独立目录中进行。

## 本机位置与下一次操作

工作目录：C:\Users\kanshan\FogScavengerTraining
官方示例工程：该目录下 ml-agents-r21\Project
训练程序：C:\Users\kanshan\anaconda3\envs\fog-mlagents\Scripts\mlagents-learn.exe
模型：工作目录下 results\r21-gpu-smoke\3DBall.onnx
构建：工作目录下 ml-agents-r21\Project\Builds\3DBall\3DBall.exe

在 VS Code 选择 fog-mlagents 解释器，再打开终端执行：

```powershell
conda activate fog-mlagents
cd C:\Users\kanshan\FogScavengerTraining
mlagents-learn 3dball-smoke.yaml --env ml-agents-r21/Project/Builds/3DBall/3DBall.exe --run-id my-first-3dball --no-graphics --seed 42
```

run-id 每次新实验用新名称；继续已有实验才用 --resume。不要覆盖 r21-gpu-smoke 的验证结果。

## 干净环境重建顺序

1. conda create -n fog-mlagents python=3.10.12 pip -y，然后激活。
2. git clone --depth 1 --branch release_21 https://github.com/Unity-Technologies/ml-agents.git ml-agents-r21；核对上述提交。
3. 在源码根目录 git apply 指向 release21-numpy.patch；如果补丁未应用，不要继续安装。
4. python -m pip install setuptools==69.5.1 wheel==0.43.0 numpy==1.23.5
5. python -m pip install torch==2.7.1 --index-url https://download.pytorch.org/whl/cu128
6. python -m pip install -r requirements-r21-lock.txt （锁文件不含本地 ML-Agents 的 editable 路径；Torch 已在上一步按官方 CUDA 源安装。）
7. 从包含 ml-agents-r21 的目录运行 python -m pip install --no-build-isolation -c constraints-r21.txt -e ./ml-agents-r21/ml-agents-envs -e ./ml-agents-r21/ml-agents
8. python -m pip check，再按本次步骤运行 GPU、构建、训练、恢复与推理验证。Unity 构建脚本放到官方 Project/Assets/Editor；推理检查需要将导出模型复制到 Project/Assets/Fog3DBall.onnx。

requirements-r21-lock.txt 记录本机完整 Python 包版本；另一台电脑的干净环境重建尚未执行，不把锁文件存在当作异机复现已通过。
