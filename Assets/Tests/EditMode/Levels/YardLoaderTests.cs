using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Levels;
using FallenYard.Progression;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Levels
{
    public class YardLoaderTests
    {
        YardDefinition _def;
        LeafField _field;
        YardProgress _progress;
        WindSystem _wind;
        LevelFlow _flow;
        YardLoader _loader;
        GameObject _player;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _def = YardLayouts.Create(YardLayouts.CornerLot);
            _def.leafCount = 3000; // keep the test quick

            _field = new GameObject("LeafField (test)").AddComponent<LeafField>();
            _field.spawnOnStart = false;
            var managers = new GameObject("Managers (test)");
            _progress = managers.AddComponent<YardProgress>();
            _wind = managers.AddComponent<WindSystem>();
            _flow = managers.AddComponent<LevelFlow>();
            _player = new GameObject("Player (test)");
            _player.AddComponent<CharacterController>();
            _player.AddComponent<FallenYard.Player.FirstPersonController>();

            _loader = new GameObject("YardLoader (test)").AddComponent<YardLoader>();
            _loader.definition = _def;
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.Call(_loader, "OnDestroy");
            Object.DestroyImmediate(_loader.gameObject);
            TestUtil.DestroyLeafField(_field);
            Object.DestroyImmediate(_progress.gameObject);
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_def);
            TestUtil.SetStaticInstance<YardLoader>(null);
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void Apply_ConfiguresTheSceneForTheYard()
        {
            _loader.Apply();

            Assert.AreEqual(_def.size, _field.yardSize);
            Assert.AreEqual(3000, _field.leafCount);
            Assert.AreEqual(_def.seed, _field.seed);
            Assert.IsInstanceOf<YardLeafSampler>(_field.SpawnSampler);
            Assert.AreEqual(_def.leafMix.Count, _field.leafTypes.Length);
            Assert.IsNotNull(_field.leafMaterials);

            Assert.AreEqual(YardLayouts.CornerLot, _progress.yardId);
            Assert.AreEqual(_def.completionBonus, _progress.completionBonus);
            Assert.AreEqual(_def.completeAtFraction, _progress.completeAtFraction);
            Assert.AreEqual(YardLayouts.CornerLot, _flow.currentYardId);
            Assert.AreEqual(_def.weather.prevailingDirection, _wind.settings.prevailingDirection);
            Assert.AreEqual(_def.weather.gustStrength, _wind.settings.gustStrength);

            var pos = _player.transform.position;
            Assert.AreEqual(_def.playerSpawn.x, pos.x, 1e-4f);
            Assert.AreEqual(_def.playerSpawn.y, pos.z, 1e-4f);
        }

        [Test]
        public void SpawnedLeaves_FollowTheYardLayout()
        {
            _loader.Apply();
            TestUtil.Call(_field, "Awake");
            _field.Spawn(_field.seed);

            Assert.AreEqual(3000, _field.TotalLeaves);
            for (int i = 0; i < _field.TotalLeaves; i++)
            {
                var p = _field.GetLeafPosition(i);
                var local = new Vector2(p.x, p.z);
                Assert.IsTrue(YardRules.IsOpenGround(_def, local, -0.05f), $"leaf {i} at {local} is inside something");
            }

            // The windy corner (weight 2.5 over 25 m²) is far denser than the yard as a whole.
            int corner = _field.CountInRadius(new Vector3(-13.5f, 0f, 10f), 2f);
            float expectedIfEven = 3000f * (Mathf.PI * 4f) / (_def.size.x * _def.size.y);
            Assert.Greater(corner, expectedIfEven * 3f);
        }

        [Test]
        public void Apply_RunsOnce()
        {
            _loader.Apply();
            _field.leafCount = 5;
            _loader.Apply();
            Assert.AreEqual(5, _field.leafCount);
            Assert.IsTrue(_loader.Applied);
        }

        [Test]
        public void Start_RaisesYardLoaded()
        {
            string loaded = null;
            GameEvents.YardLoaded += id => loaded = id;
            TestUtil.Call(_loader, "Start");
            Assert.AreEqual(YardLayouts.CornerLot, loaded);
        }
    }
}
