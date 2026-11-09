using System;
using System.Collections.Generic;
using FallenYard.Leaves;
using UnityEngine;

namespace FallenYard.Levels
{
    /// <summary>
    /// Spawns a yard's leaves by its zones: some spread evenly, most piled under trees and
    /// against hedges, none inside houses, trunks or the compost bin.
    /// </summary>
    public class YardLeafSampler : ILeafSpawnSampler
    {
        const int Attempts = 16;

        readonly YardDefinition _def;
        readonly List<LeafSpawnZone> _zones = new List<LeafSpawnZone>();
        readonly List<float> _cumulative = new List<float>();
        readonly float _total;

        public YardLeafSampler(YardDefinition def)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            float sum = Mathf.Max(0f, def.backgroundWeight);
            _zones.Add(null); // index 0 = the whole yard
            _cumulative.Add(sum);
            foreach (var z in def.spawnZones)
            {
                if (z == null || z.weight <= 0f) continue;
                sum += z.weight;
                _zones.Add(z);
                _cumulative.Add(sum);
            }
            _total = sum;
        }

        public Vector2 SamplePosition(System.Random rng)
        {
            Vector2 p = Vector2.zero;
            for (int i = 0; i < Attempts; i++)
            {
                p = SampleZone(PickZone(rng), rng);
                if (YardRules.IsOpenGround(_def, p)) return p;
            }
            // Crowded zone: fall back to anywhere open.
            for (int i = 0; i < Attempts; i++)
            {
                p = SampleZone(null, rng);
                if (YardRules.IsOpenGround(_def, p)) return p;
            }
            return ClampToYard(p);
        }

        LeafSpawnZone PickZone(System.Random rng)
        {
            if (_total <= 0f) return null;
            float r = (float)rng.NextDouble() * _total;
            for (int i = 0; i < _cumulative.Count; i++)
                if (r < _cumulative[i]) return _zones[i];
            return _zones[_zones.Count - 1];
        }

        Vector2 SampleZone(LeafSpawnZone zone, System.Random rng)
        {
            float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
            if (zone == null)
                return new Vector2((u - 0.5f) * _def.size.x, (v - 0.5f) * _def.size.y);
            if (zone.shape == ZoneShape.Circle)
            {
                // sqrt for an even spread over the disc, then a gentle bias to the middle so piles read as piles.
                float r = zone.size.x * Mathf.Pow(u, 0.65f);
                float a = v * Mathf.PI * 2f;
                return ClampToYard(zone.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return ClampToYard(zone.center + new Vector2((u - 0.5f) * zone.size.x, (v - 0.5f) * zone.size.y));
        }

        Vector2 ClampToYard(Vector2 p)
        {
            float hx = _def.size.x * 0.5f, hz = _def.size.y * 0.5f;
            return new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, -hz, hz));
        }
    }
}
