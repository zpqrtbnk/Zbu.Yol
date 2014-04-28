using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Zbu.Yol;

namespace Zbu.Yol.Tests
{
    [TestFixture]
    public class TransitionsGraph
    {
        [Test]
        public void CanDefine()
        {
            var mgr = new YolManager();
            mgr.DefineTransition(string.Empty, "aaa", () => true);
            mgr.DefineTransition("aaa", "bbb", () => true);
            mgr.DefineTransition("bbb", "ccc", () => true);
        }

        [Test]
        public void CannotTransitionToSameState()
        {
            var mgr = new YolManager();
            Assert.Throws<ArgumentException>(() =>
            {
                mgr.DefineTransition("aaa", "aaa", () => true);
            });
        }

        [Test]
        public void OnlyOneTransitionPerState()
        {
            var mgr = new YolManager();
            mgr.DefineTransition("aaa", "bbb", () => true);
            Assert.Throws<InvalidOperationException>(() =>
            {
                mgr.DefineTransition("aaa", "ccc", () => true);
            });
        }

        [Test]
        public void CannotContainTwoMoreHeads()
        {
            var mgr = new YolManager();
            mgr.DefineTransition(string.Empty, "aaa", () => true);
            mgr.DefineTransition("aaa", "bbb", () => true);
            mgr.DefineTransition("ccc", "ddd", () => true);
            Assert.Throws<Exception>(mgr.ValidateTransitions);
        }

        [Test]
        public void CannotContainLoops()
        {
            var mgr = new YolManager();
            mgr.DefineTransition(string.Empty, "aaa", () => true);
            mgr.DefineTransition("aaa", "bbb", () => true);
            mgr.DefineTransition("bbb", "ccc", () => true);
            mgr.DefineTransition("ccc", "aaa", () => true);
            Assert.Throws<Exception>(mgr.ValidateTransitions);
        }
    }
}
