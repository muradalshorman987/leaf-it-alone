using FallenYard.UI;
using NUnit.Framework;

namespace FallenYard.Tests.UI
{
    public class MenuAndToastViewTests
    {
        [Test]
        public void MenuScreen_BuildsButtonsAndToggles()
        {
            var menu = new MenuScreen("pause", "Paused", "sub");
            menu.AddButton("pause-resume", "Resume", () => { }, primary: true);
            menu.AddButton("pause-quit", "Quit", () => { });

            Assert.IsFalse(menu.Visible, "menus start hidden");
            Assert.AreEqual(2, menu.Buttons.Count);
            Assert.AreEqual("Resume", menu.Buttons[0].text);
            Assert.IsTrue(menu.Buttons[0].ClassListContains("fy-button--primary"));
            Assert.AreEqual("Paused", menu.TitleLabel.text);

            menu.Show();
            Assert.IsTrue(menu.Visible);
            menu.Hide();
            Assert.IsFalse(menu.Visible);
        }

        [Test]
        public void MenuScreen_SubtitleHiddenWhenEmpty()
        {
            var menu = new MenuScreen("m", "Title");
            Assert.IsFalse(UIViewUtil.IsVisible(menu.SubtitleLabel));
            menu.SetSubtitle("hello");
            Assert.IsTrue(UIViewUtil.IsVisible(menu.SubtitleLabel));
        }

        [Test]
        public void ToastView_MirrorsQueue()
        {
            var queue = new ToastQueue();
            queue.Push("existing", 1f);
            var view = new ToastView();
            view.Bind(queue);
            Assert.AreEqual(1, view.Count);

            queue.Push("new", 5f);
            Assert.AreEqual(2, view.Root.childCount);

            queue.Tick(2f);
            Assert.AreEqual(1, view.Count);
            Assert.AreEqual(1, view.Root.childCount);

            view.Unbind();
            queue.Push("after unbind");
            Assert.AreEqual(0, view.Count);
        }
    }
}
