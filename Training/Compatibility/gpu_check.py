import torch

print("PyTorch:", torch.__version__)
print("CUDA available:", torch.cuda.is_available())
assert torch.cuda.is_available(), "CUDA unavailable"

print("GPU:", torch.cuda.get_device_name(0))

x = torch.randn(1024, 1024, device="cuda", requires_grad=True)
loss = (x @ x).square().mean()
loss.backward()
torch.cuda.synchronize()

assert torch.isfinite(loss).item(), "Loss is not finite"
assert torch.isfinite(x.grad).all().item(), "Gradient is not finite"

print("Device:", x.device)
print("Loss:", loss.item())
print("GPU forward/backward: PASS")