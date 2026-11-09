using FallenYard.UI;
using NUnit.Framework;

namespace FallenYard.Tests.UI
{
    public class ToastQueueTests
    {
        [Test]
        public void Push_AddsAndRaises()
        {
            var q = new ToastQueue();
            ToastQueue.Toast added = null;
            q.Added += t => added = t;
            var toast = q.Push("hello", 2f);
            Assert.AreSame(toast, added);
            Assert.AreEqual(1, q.Active.Count);
        }

        [Test]
        public void Push_EmptyText_Ignored()
        {
            var q = new ToastQueue();
            Assert.IsNull(q.Push(""));
            Assert.IsNull(q.Push(null));
            Assert.AreEqual(0, q.Active.Count);
        }

        [Test]
        public void Tick_ExpiresToasts()
        {
            var q = new ToastQueue();
            int removed = 0;
            q.Removed += _ => removed++;
            q.Push("short", 1f);
            q.Push("long", 3f);
            q.Tick(1.5f);
            Assert.AreEqual(1, q.Active.Count);
            Assert.AreEqual("long", q.Active[0].Text);
            Assert.AreEqual(1, removed);
            Assert.AreEqual(0.5f, q.Active[0].Progress, 1e-4f);
        }

        [Test]
        public void Push_OverCapacity_DropsOldest()
        {
            var q = new ToastQueue(2);
            q.Push("a");
            q.Push("b");
            q.Push("c");
            Assert.AreEqual(2, q.Active.Count);
            Assert.AreEqual("b", q.Active[0].Text);
            Assert.AreEqual("c", q.Active[1].Text);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var q = new ToastQueue();
            int removed = 0;
            q.Removed += _ => removed++;
            q.Push("a");
            q.Push("b");
            q.Clear();
            Assert.AreEqual(0, q.Active.Count);
            Assert.AreEqual(2, removed);
        }
    }
}
