using FallenYard.Accessibility;
using FallenYard.Core;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Accessibility
{
    public class CaptionTests
    {
        CaptionDirector _director;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            CaptionCatalog.ClearRegistered();
            _director = new CaptionDirector { Enabled = true };
            _director.Bind();
        }

        [TearDown]
        public void TearDown()
        {
            _director.Unbind();
            GameEvents.ClearAllSubscribers();
            CaptionCatalog.ClearRegistered();
        }

        [Test]
        public void Queue_SameIdRefreshesInsteadOfStacking()
        {
            var q = new CaptionQueue();
            int added = 0, updated = 0;
            q.Added += _ => added++;
            q.Updated += _ => updated++;
            q.Show("blower", "[Blower]", 1f);
            q.Tick(0.6f);
            q.Show("blower", "[Blower]", 1f);
            Assert.AreEqual(1, q.Lines.Count);
            Assert.AreEqual(1, added);
            Assert.AreEqual(1, updated);
            Assert.AreEqual(1f, q.Lines[0].Remaining, 1e-4f, "refresh restores the time");
        }

        [Test]
        public void Queue_DropsOldestWhenFull_AndExpires()
        {
            var q = new CaptionQueue(2);
            q.Show("a", "A", 1f);
            q.Show("b", "B", 3f);
            q.Show("c", "C", 3f);
            Assert.AreEqual(2, q.Lines.Count);
            Assert.AreEqual("B", q.Lines[0].Text);
            q.Tick(3.1f);
            Assert.AreEqual(0, q.Lines.Count);
            Assert.IsNull(q.Show("x", "", 1f), "empty text is ignored");
        }

        [Test]
        public void Disabled_ShowsNothing()
        {
            _director.Enabled = false;
            GameEvents.RaiseAudioCuePlayed(CaptionCatalog.Birds);
            GameEvents.RaisePileBagged(100, 50);
            Assert.AreEqual(0, _director.Queue.Lines.Count);
        }

        [Test]
        public void AudioCue_KnownIdsCaption_UnknownAndUiIgnored()
        {
            GameEvents.RaiseAudioCuePlayed(CaptionCatalog.Birds);
            GameEvents.RaiseAudioCuePlayed("mystery_noise");
            GameEvents.RaiseAudioCuePlayed("UiClick");
            Assert.AreEqual(1, _director.Queue.Lines.Count);
            Assert.AreEqual("[Birds chirping]", _director.Queue.Lines[0].Text);
        }

        [Test]
        public void Register_AddsNewCues()
        {
            CaptionCatalog.Register("owl", "[Owl hoots]");
            GameEvents.RaiseAudioCuePlayed("owl");
            Assert.AreEqual("[Owl hoots]", _director.Queue.Lines[0].Text);
        }

        [Test]
        public void PileBagged_CaptionsBagAndCoins()
        {
            GameEvents.RaisePileBagged(80, 40);
            Assert.AreEqual(2, _director.Queue.Lines.Count);
            Assert.IsNotNull(_director.Queue.Find(CaptionCatalog.BagRustle));
            Assert.IsNotNull(_director.Queue.Find(CaptionCatalog.CoinChime));
        }

        [Test]
        public void AudioAndGameplayCues_ShareOneLine()
        {
            GameEvents.RaisePileBagged(80, 0);
            GameEvents.RaiseAudioCuePlayed(CaptionCatalog.BagRustle);
            Assert.AreEqual(1, _director.Queue.Lines.Count);
        }

        [Test]
        public void Catalog_CoversEveryAudibleWp8Cue()
        {
            foreach (FallenYard.Audio.SoundCue cue in System.Enum.GetValues(typeof(FallenYard.Audio.SoundCue)))
            {
                string id = cue.ToString();
                bool silentOnPurpose = id == "UiClick" || id == "ToolEquip" || id == "WindBed" || id.StartsWith("Music");
                Assert.AreEqual(!silentOnPurpose, CaptionCatalog.TryGet(id, out _), id);
            }
        }

        [Test]
        public void ToolThrottle_CaptionsTheEquippedTool()
        {
            GameEvents.RaiseToolEquipped("blower");
            GameEvents.RaiseToolThrottle(0.1f);
            Assert.AreEqual(0, _director.Queue.Lines.Count, "idle throttle is quiet");
            GameEvents.RaiseToolThrottle(0.9f);
            Assert.AreEqual("[Leaf blower roaring]", _director.Queue.Lines[0].Text);
        }

        [Test]
        public void WindGust_SaysWhereItComesFrom()
        {
            _director.ListenerForward = () => Vector3.forward;
            GameEvents.RaiseWindGust(Vector3.right, 0.1f);
            Assert.AreEqual(0, _director.Queue.Lines.Count, "weak gusts are not captioned");
            GameEvents.RaiseWindGust(Vector3.right, 1f); // blowing to the right = coming from the left
            Assert.AreEqual("[Wind gusts from the left]", _director.Queue.Lines[0].Text);
        }

        [Test]
        public void GustSide_CoversAllDirections()
        {
            Assert.AreEqual("from the left", CaptionDirector.GustSide(Vector3.forward, Vector3.right));
            Assert.AreEqual("from the right", CaptionDirector.GustSide(Vector3.forward, Vector3.left));
            Assert.AreEqual("ahead", CaptionDirector.GustSide(Vector3.forward, Vector3.back));
            Assert.AreEqual("from behind", CaptionDirector.GustSide(Vector3.forward, Vector3.forward));
            Assert.IsNull(CaptionDirector.GustSide(Vector3.up, Vector3.right), "looking straight up has no heading");
        }

        [Test]
        public void CompostReady_CaptionsOnlyWhenABatchFinishes()
        {
            GameEvents.RaiseCompostChanged(0.5f, 0);
            Assert.AreEqual(0, _director.Queue.Lines.Count);
            GameEvents.RaiseCompostChanged(0f, 1);
            Assert.IsNotNull(_director.Queue.Find(CaptionCatalog.CompostReady));
        }

        [Test]
        public void YardCompleteAndUpgrade_AreCaptioned()
        {
            GameEvents.RaiseToolUpgraded("rake", 2);
            GameEvents.RaiseYardCompleted("yard_01");
            Assert.IsNotNull(_director.Queue.Find(CaptionCatalog.UpgradeChime));
            Assert.IsNotNull(_director.Queue.Find(CaptionCatalog.YardComplete));
        }

        [Test]
        public void Unbind_StopsListening()
        {
            _director.Unbind();
            GameEvents.RaiseAudioCuePlayed(CaptionCatalog.Birds);
            Assert.AreEqual(0, _director.Queue.Lines.Count);
        }
    }
}
