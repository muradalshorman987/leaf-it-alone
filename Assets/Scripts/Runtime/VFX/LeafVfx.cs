using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Tools;
using UnityEngine;

namespace FallenYard.VFX
{
    /// <summary>
    /// Leaf and tool particles: blower air streaks and dust, vacuum intake, rake scuffs,
    /// the bagging poof, loose leaves drifting on the wind, and a leaf flurry on gusts
    /// and yard completion.
    ///
    /// Perf: four emit-only particle systems with hard caps (well under 1,000 particles
    /// in total), leaf particles are instanced meshes, and nothing reads or writes the
    /// LeafField, so the 30k-leaf Burst budget is untouched.
    /// </summary>
    public class LeafVfx : MonoBehaviour
    {
        public static LeafVfx Instance { get; private set; }

        [Header("References (found if empty)")]
        public Camera playerCamera;
        public ToolController tools;
        public WindSystem wind;

        [Header("Materials (built at runtime if empty; the setup menu assigns assets for builds)")]
        public Material dustMaterial;
        public Material leafMaterial;

        [Header("Look")]
        public Color[] leafColors =
        {
            new Color(0.88f, 0.42f, 0.10f), new Color(0.72f, 0.18f, 0.09f),
            new Color(0.94f, 0.70f, 0.18f), new Color(0.55f, 0.32f, 0.13f),
        };
        public Color dustColor = new Color(0.66f, 0.56f, 0.42f, 0.35f);
        public Color streakColor = new Color(1f, 1f, 1f, 0.22f);
        [Tooltip("Layers the tool dust raycast can hit.")]
        public LayerMask groundMask = ~0;

        [Header("Rates (per second at full effort)")]
        [Range(0f, 2f)] public float density = 1f;
        public float blowerStreakRate = 110f;
        public float blowerDustRate = 26f;
        public float vacuumStreakRate = 70f;
        public float rakeDustRate = 12f;
        public float airborneRate = 22f;

        [Header("Bag poof")]
        public int poofMinLeaves = 10;
        public int poofMaxLeaves = 55;

        [Header("Budget (max live particles per system)")]
        public int maxStreaks = 220;
        public int maxDust = 160;
        public int maxLeaves = 260;

        [Tooltip("Loose leaves spawn this far around the player (min, max metres).")]
        public Vector2 airborneRing = new Vector2(3f, 12f);

        public ParticleSystem Streaks { get; private set; }
        public ParticleSystem Dust { get; private set; }
        public ParticleSystem Leaves { get; private set; }
        public int PoofsPlayed { get; private set; }
        public int LastPoofCount { get; private set; }

        float _streakCarry, _dustCarry, _airCarry;
        Mesh _leafMesh;
        Material _ownedDust, _ownedLeaf;

        void Awake()
        {
            Instance = this;
            if (playerCamera == null) playerCamera = Camera.main;
            if (tools == null) tools = FindFirstObjectByType<ToolController>();
            if (wind == null) wind = FindFirstObjectByType<WindSystem>();
            Build();
        }

        void OnEnable()
        {
            GameEvents.LeavesBaggedAt += OnLeavesBagged;
            GameEvents.WindGust += OnGust;
            GameEvents.YardCompleted += OnYardCompleted;
        }

        void OnDisable()
        {
            GameEvents.LeavesBaggedAt -= OnLeavesBagged;
            GameEvents.WindGust -= OnGust;
            GameEvents.YardCompleted -= OnYardCompleted;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyOwned(_leafMesh);
            DestroyOwned(_ownedDust);
            DestroyOwned(_ownedLeaf);
        }

        void Build()
        {
            if (Streaks != null) return;
            if (dustMaterial == null) dustMaterial = _ownedDust = ParticleFactory.CreateDustMaterial();
            if (leafMaterial == null) leafMaterial = _ownedLeaf = ParticleFactory.CreateLeafMaterial();

            // Air streaks: thin stretched billboards that show the blower's cone.
            Streaks = ParticleFactory.Create("VFX_Streaks", transform, maxStreaks, dustMaterial);
            var sm = Streaks.main;
            sm.startColor = streakColor;
            sm.startSize = 0.04f;
            sm.startLifetime = 0.3f;
            ParticleFactory.FadeOverLife(Streaks);
            var sr = Streaks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.06f;
            sr.lengthScale = 2f;

            // Dust: soft puffs that grow and fade.
            Dust = ParticleFactory.Create("VFX_Dust", transform, maxDust, dustMaterial);
            var dm = Dust.main;
            dm.startColor = dustColor;
            dm.startSize = 0.4f;
            dm.startLifetime = 1.2f;
            dm.gravityModifier = -0.02f;
            ParticleFactory.FadeOverLife(Dust);
            ParticleFactory.GrowOverLife(Dust, 0.5f, 1.6f);
            var drag = Dust.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;

            // Leaves: instanced leaf meshes tumbling in 3D.
            _leafMesh = LeafMeshFactory.CreateLeafMesh();
            Leaves = ParticleFactory.Create("VFX_Leaves", transform, maxLeaves, leafMaterial);
            var lm = Leaves.main;
            lm.startSize = 0.13f;
            lm.startLifetime = 3f;
            lm.startRotation3D = true;
            lm.gravityModifier = 0.22f;
            ParticleFactory.Flutter(Leaves, 0.7f, 0.6f);
            var rot = Leaves.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var ldrag = Leaves.limitVelocityOverLifetime;
            ldrag.enabled = true;
            ldrag.drag = 0.8f;
            var fade = Leaves.sizeOverLifetime; // shrink away at the end instead of alpha (opaque material)
            fade.enabled = true;
            fade.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.1f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
            var collision = Leaves.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.collidesWith = groundMask;
            collision.dampen = 0.85f;
            collision.bounce = 0.05f;
            var lr = Leaves.GetComponent<ParticleSystemRenderer>();
            lr.renderMode = ParticleSystemRenderMode.Mesh;
            lr.mesh = _leafMesh;
            lr.alignment = ParticleSystemRenderSpace.World;
            lr.enableGPUInstancing = true;

            foreach (var ps in new[] { Streaks, Dust, Leaves }) ps.Play(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null) return;
            UpdateTool(playerCamera.transform, dt);
            UpdateAirborne(playerCamera.transform, dt);
        }

        void UpdateTool(Transform cam, float dt)
        {
            var tool = tools != null ? tools.Current : null;
            float throttle = tool != null ? tool.Throttle : 0f;
            if (throttle <= 0.01f)
            {
                _streakCarry = _dustCarry = 0f;
                return;
            }

            if (tool is LeafBlower blower)
            {
                Vector3 nozzle = Nozzle(cam, blower.nozzleOffset);
                int n = VfxMath.Accumulate(ref _streakCarry, VfxMath.BlowerRate(throttle, blowerStreakRate * density), dt);
                float speedScale = Mathf.Lerp(0.5f, 1f, throttle);
                for (int i = 0; i < n; i++)
                {
                    Vector3 dir = VfxMath.ConeDirection(cam.forward, blower.coneHalfAngle * 0.7f, Random.value, Random.value);
                    EmitStreak(nozzle + dir * 0.15f, dir * Random.Range(9f, 14f) * speedScale, Random.Range(0.22f, 0.38f));
                }
                if (TryGround(cam, blower.baseRange * 1.3f, out var hit))
                {
                    int d = VfxMath.Accumulate(ref _dustCarry, VfxMath.DustRate(throttle, blowerDustRate * density), dt);
                    Vector3 push = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                    for (int i = 0; i < d; i++)
                        EmitDust(hit + Random.insideUnitSphere * 0.4f, push * Random.Range(1f, 2.5f) + Vector3.up * Random.Range(0.3f, 0.9f));
                }
            }
            else if (tool is LeafVacuum vacuum)
            {
                Vector3 nozzle = Nozzle(cam, new Vector3(0f, -0.5f, 0.4f));
                int n = VfxMath.Accumulate(ref _streakCarry, VfxMath.BlowerRate(throttle, vacuumStreakRate * density), dt);
                float reach = Mathf.Max(1f, vacuum.baseRange);
                for (int i = 0; i < n; i++)
                {
                    Vector3 dir = VfxMath.ConeDirection(cam.forward, 18f, Random.value, Random.value);
                    float life = Random.Range(0.25f, 0.4f);
                    Vector3 start = nozzle + dir * reach * Random.Range(0.6f, 1f);
                    EmitStreak(start, (nozzle - start) / life, life);
                }
            }
            else if (TryGround(cam, 3.5f, out var hit))
            {
                // Rake and mulcher: little scuffs of dust where they work.
                int d = VfxMath.Accumulate(ref _dustCarry, VfxMath.DustRate(throttle, rakeDustRate * density), dt);
                for (int i = 0; i < d; i++)
                    EmitDust(hit + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 0.4f + Vector3.up * 0.4f);
            }
        }

        void UpdateAirborne(Transform cam, float dt)
        {
            float strength = wind != null ? wind.WindStrength : 0.6f;
            var weather = WeatherSystem.Instance != null ? WeatherSystem.Instance.Current : WeatherSample.Calm;
            float rate = VfxMath.AirborneRate(strength, weather.airborneLeaves, weather.gustFlash, airborneRate * density);
            int n = VfxMath.Accumulate(ref _airCarry, rate, dt);
            for (int i = 0; i < n; i++) EmitDrifter(cam.position, strength);
        }

        /// <summary>Bagging poof: leaves and dust thrown up from the pile.</summary>
        public void Poof(Vector3 position, int leavesBagged)
        {
            int count = Mathf.RoundToInt(VfxMath.PoofCount(leavesBagged, poofMinLeaves, poofMaxLeaves) * density);
            if (count <= 0) return;
            PoofsPlayed++;
            LastPoofCount = count;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = VfxMath.ConeDirection(Vector3.up, 55f, Random.value, Random.value);
                EmitLeaf(position + Random.insideUnitSphere * 0.3f + Vector3.up * 0.2f, dir * Random.Range(2f, 4.5f), Random.Range(1.6f, 2.6f));
            }
            int dust = Mathf.Max(3, count / 3);
            for (int i = 0; i < dust; i++)
            {
                Vector3 v = Random.insideUnitSphere * 1.2f;
                v.y = Mathf.Abs(v.y) + 0.3f;
                EmitDust(position + Random.insideUnitSphere * 0.4f, v, 1.4f, 0.7f);
            }
        }

        void OnLeavesBagged(Vector3 position, int leaves) => Poof(position, leaves);

        void OnGust(Vector3 direction, float strength)
        {
            if (playerCamera == null) return;
            int n = Mathf.RoundToInt(Mathf.Clamp(strength * 4f, 6f, 30f) * density);
            for (int i = 0; i < n; i++) EmitDrifter(playerCamera.transform.position, Mathf.Max(strength, 2f), direction);
        }

        void OnYardCompleted(string yardId)
        {
            if (playerCamera == null) return;
            var t = playerCamera.transform;
            Vector3 ahead = t.position + Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized * 3f;
            Poof(ahead + t.right * 1.5f, 400);
            Poof(ahead - t.right * 1.5f, 400);
        }

        void EmitDrifter(Vector3 around, float windStrength, Vector3? directionOverride = null)
        {
            Vector3 dir = directionOverride ?? (wind != null ? wind.WindDirection : Vector3.forward);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(airborneRing.x, airborneRing.y);
            Vector3 pos = around + new Vector3(ring.x, 0f, ring.y) - dir * 4f;
            pos.y = around.y + Random.Range(-1f, 2.5f);
            Vector3 vel = dir * windStrength * Random.Range(0.5f, 0.9f) + Vector3.up * Random.Range(0.1f, 0.8f);
            EmitLeaf(pos, vel, Random.Range(4f, 7f));
        }

        void EmitStreak(Vector3 pos, Vector3 vel, float life)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startLifetime = life,
                startSize = Random.Range(0.025f, 0.06f), applyShapeToPosition = false,
            };
            Streaks.Emit(ep, 1);
        }

        void EmitDust(Vector3 pos, Vector3 vel, float life = 1.1f, float size = 0.45f)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startLifetime = life * Random.Range(0.8f, 1.25f),
                startSize = size * Random.Range(0.7f, 1.3f), applyShapeToPosition = false,
            };
            Dust.Emit(ep, 1);
        }

        void EmitLeaf(Vector3 pos, Vector3 vel, float life)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startLifetime = life,
                startSize = Random.Range(0.1f, 0.17f), // leaf meshes are 1 m long
                rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                startColor = LeafColor(), applyShapeToPosition = false,
            };
            Leaves.Emit(ep, 1);
        }

        Color LeafColor()
        {
            if (leafColors == null || leafColors.Length == 0) return new Color(0.85f, 0.45f, 0.12f);
            Color c = leafColors[Random.Range(0, leafColors.Length)];
            float shade = Random.Range(0.85f, 1.1f);
            return new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
        }

        static Vector3 Nozzle(Transform cam, Vector3 offset) =>
            cam.position + cam.right * offset.x + cam.up * offset.y + cam.forward * offset.z;

        bool TryGround(Transform cam, float range, out Vector3 point)
        {
            if (Physics.Raycast(cam.position, cam.forward, out var hit, range, groundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        static void DestroyOwned(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
