using System.Linq;
using FallenYard.Core;
using FallenYard.Levels;
using FallenYard.Progression;
using FallenYard.Tests.Levels;
using FallenYard.UI;
using NUnit.Framework;

namespace FallenYard.Tests.UI
{
    public class YardMapTests
    {
        YardCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _catalog = TownFixture.Create();
        }

        [TearDown]
        public void TearDown()
        {
            TownFixture.Destroy(_catalog);
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void NewGame_OnlyTheStarterLawnIsOpen()
        {
            var data = TownFixture.FreshSave(_catalog);
            var rows = YardMapModel.BuildRows(_catalog, data, YardLayouts.StarterLawn, 0);
            Assert.AreEqual(5, rows.Count);
            Assert.AreEqual(YardMapAction.Current, rows[0].Action);
            Assert.AreEqual("You're here", rows[0].ButtonText);
            Assert.AreEqual("New", rows[0].StatusText);
            Assert.IsTrue(rows.Skip(1).All(r => r.Action == YardMapAction.Locked && !r.Interactable));
            Assert.AreEqual("Clean Starter Lawn first", rows[1].StatusText);
            Assert.AreEqual("Gran Odile", rows[0].Neighbour);
        }

        [Test]
        public void Progress_OpensPlayAndBuyRows()
        {
            var data = TownFixture.FreshSave(_catalog);
            MetaProgression.RecordCompletion(data, _catalog.unlockTable, YardLayouts.StarterLawn);
            MetaProgression.RecordCompletion(data, _catalog.unlockTable, YardLayouts.CornerLot);

            var poor = YardMapModel.BuildRows(_catalog, data, YardLayouts.CornerLot, 100);
            Assert.AreEqual(YardMapAction.Play, poor[0].Action);
            Assert.AreEqual("Cleaned", poor[0].StatusText);
            Assert.AreEqual("Visit", poor[0].ButtonText);
            Assert.AreEqual(YardMapAction.Current, poor[1].Action);
            Assert.AreEqual(YardMapAction.Buy, poor[2].Action);
            Assert.AreEqual(300, poor[2].Cost);
            Assert.IsFalse(poor[2].CanAfford);
            Assert.IsFalse(poor[2].Interactable);
            Assert.AreEqual("Open for 300", poor[2].ButtonText);

            var rich = YardMapModel.BuildRows(_catalog, data, YardLayouts.CornerLot, 500);
            Assert.IsTrue(rich[2].Interactable);
        }

        [Test]
        public void PartlyCleanedYard_ShowsBestPercent()
        {
            var data = TownFixture.FreshSave(_catalog);
            MetaProgression.RecordProgress(data, YardLayouts.StarterLawn, 0.62f);
            var rows = YardMapModel.BuildRows(_catalog, data, YardLayouts.CornerLot, 0);
            Assert.AreEqual("Best 62%", rows[0].StatusText);
            Assert.AreEqual("Go rake", rows[0].ButtonText);
        }

        [Test]
        public void NoCatalog_NoRows()
        {
            CollectionAssert.IsEmpty(YardMapModel.BuildRows(null, null, null, 0));
        }

        [Test]
        public void NextYardButtonText_DependsOnStatus()
        {
            var corner = _catalog.Get(YardLayouts.CornerLot);
            Assert.AreEqual("On to Corner Lot", YardMapModel.NextYardButtonText(corner, YardStatus.Unlocked, 0));
            Assert.AreEqual("Open Corner Lot (300)", YardMapModel.NextYardButtonText(corner, YardStatus.Purchasable, 300));
            Assert.IsNull(YardMapModel.NextYardButtonText(corner, YardStatus.Locked, 0));
            Assert.IsNull(YardMapModel.NextYardButtonText(null, YardStatus.Unlocked, 0));
        }

        [Test]
        public void View_BuildsOneButtonPerYard()
        {
            var data = TownFixture.FreshSave(_catalog);
            MetaProgression.RecordCompletion(data, _catalog.unlockTable, YardLayouts.StarterLawn);
            var view = new YardMapView();
            view.Refresh(YardMapModel.BuildRows(_catalog, data, YardLayouts.StarterLawn, 0), 0);

            Assert.AreEqual(5, view.RowButtons.Count);
            Assert.IsTrue(view.RowButtons[YardLayouts.CornerLot].enabledSelf);
            Assert.IsTrue(view.RowButtons[YardLayouts.CornerLot].ClassListContains("fy-button--primary"));
            Assert.IsFalse(view.RowButtons[YardLayouts.HilltopManor].enabledSelf);
            Assert.IsFalse(view.Visible);
            view.Show();
            Assert.IsTrue(view.Visible);

        }

        [Test]
        public void UIFlow_YardMapReturnsWhereItCameFrom()
        {
            var flow = new UIFlow(GamePhase.TitleMenu);
            Assert.IsTrue(flow.OpenYardMap());
            Assert.AreEqual(GamePhase.YardMap, flow.Phase);
            Assert.IsTrue(UIFlow.BlocksInput(GamePhase.YardMap));
            Assert.IsTrue(UIFlow.FreezesTime(GamePhase.YardMap));
            Assert.IsTrue(flow.CloseYardMap());
            Assert.AreEqual(GamePhase.TitleMenu, flow.Phase);

            flow.StartGame();
            flow.TogglePause();
            flow.OpenYardMap();
            Assert.IsTrue(flow.TogglePause(), "Escape closes the map");
            Assert.AreEqual(GamePhase.Paused, flow.Phase);

            flow.Resume();
            flow.OpenYardMap();
            Assert.IsTrue(flow.Resume());
            Assert.AreEqual(GamePhase.Playing, flow.Phase);

            Assert.IsFalse(flow.CloseYardMap(), "not open");
            flow.SetPhase(GamePhase.Shop);
            Assert.IsFalse(flow.OpenYardMap(), "not from the shop");
        }

        [Test]
        public void ArrivalNote_Format()
        {
            Assert.AreEqual("Orchard. A note from Mr. Halloran: \"Hi\"", HudFormat.ArrivalNote("Orchard", "Mr. Halloran", "Hi"));
            Assert.AreEqual("A note from the neighbour: \"Hi\"", HudFormat.ArrivalNote(null, null, "Hi"));
            Assert.AreEqual("Orchard", HudFormat.ArrivalNote("Orchard", "x", ""));
            Assert.IsNull(HudFormat.ArrivalNote(null, null, null));
        }
    }
}
