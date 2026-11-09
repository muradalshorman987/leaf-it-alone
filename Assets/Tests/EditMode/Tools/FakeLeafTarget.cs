using System.Collections.Generic;
using FallenYard.Tools;
using UnityEngine;

namespace FallenYard.Tests.Tools
{
    /// <summary>Records tool calls instead of moving real leaves.</summary>
    public class FakeLeafTarget : ILeafTarget
    {
        public struct PushCall { public Vector3 center; public float radius; public Vector3 target; public float strength; }
        public struct ConeCall { public Vector3 origin; public Vector3 dir; public float range; public float halfAngle; public float force; public float lift; }

        public readonly List<PushCall> Pushes = new List<PushCall>();
        public readonly List<ConeCall> Cones = new List<ConeCall>();
        public readonly List<int> CollectRequests = new List<int>();

        /// <summary>Leaves the fake can hand out to CollectInRadius.</summary>
        public int Available = 1000;
        public int Collected;
        public float Ground;

        public void Push(Vector3 center, float radius, Vector3 targetVelocity, float strength, float dt) =>
            Pushes.Add(new PushCall { center = center, radius = radius, target = targetVelocity, strength = strength });

        public void BlowCone(Vector3 origin, Vector3 dir, float range, float halfAngleDeg, float force, float lift, float dt) =>
            Cones.Add(new ConeCall { origin = origin, dir = dir, range = range, halfAngle = halfAngleDeg, force = force, lift = lift });

        public int CountInRadius(Vector3 center, float radius) => Available;

        public int CollectInRadius(Vector3 center, float radius, int maxLeaves)
        {
            CollectRequests.Add(maxLeaves);
            int n = Mathf.Clamp(maxLeaves, 0, Available);
            Available -= n;
            Collected += n;
            return n;
        }

        public float GroundHeightAt(Vector3 world) => Ground;

        public void ClearCalls()
        {
            Pushes.Clear();
            Cones.Clear();
            CollectRequests.Clear();
        }
    }
}
