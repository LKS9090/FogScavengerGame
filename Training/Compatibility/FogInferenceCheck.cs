using System;
using System.IO;
using UnityEditor;
using Unity.Sentis;
public static class FogInferenceCheck
{
    public static void Run()
    {
        var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Fog3DBall.onnx");
        var model = ModelLoader.Load(asset);
        if (model.inputs.Count != 1 || model.inputs[0].name != "obs_0") throw new Exception("Unexpected input schema");
        using (var worker = WorkerFactory.CreateWorker(BackendType.CPU, model))
        {
            for (int sample = 0; sample < 100; sample++)
            {
                var values = new float[8];
                for (int i = 0; i < 8; i++) values[i] = (float)Math.Sin(sample * 0.1 + i);
                using (var input = new TensorFloat(new TensorShape(1, 8), values))
                {
                    worker.Execute(input);
                    var output = worker.PeekOutput("deterministic_continuous_actions") as TensorFloat;
                    output.MakeReadable();
                    var actions = output.ToReadOnlyArray();
                    if (actions.Length != 2) throw new Exception("Wrong action count");
                    foreach (var action in actions)
                        if (float.IsNaN(action) || float.IsInfinity(action)) throw new Exception("Nonfinite action");
                }
            }
        }
        File.WriteAllText("sentis-check-ok.txt", "PASS: exported model imported by Sentis 1.2; CPU execution on 100 fixed 8-value inputs returned 2 finite actions each.");
    }
}
