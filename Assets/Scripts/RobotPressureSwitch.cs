using UnityEngine;

// Shared by the playable prototype and local motor training. Coordinates are world-space feet.
public static class RobotPressureSwitch
{
    public const float ActivationRadius = 0.72f;
    public static bool IsPressed(Vector3 robotFeet, Vector3 plateCenter)
        => Vector3.Distance(robotFeet, plateCenter) < ActivationRadius;
}
