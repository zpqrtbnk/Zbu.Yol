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
            mgr.DefineTransition(string.Empty, "aaa", (appContext, server) => true);
            mgr.DefineTransition("aaa", "bbb", (appContext, server) => true);
            mgr.DefineTransition("bbb", "ccc", (appContext, server) => true);
        }

        [Test]
        public void CannotTransitionToSameState()
        {
            var mgr = new YolManager();
            Assert.Throws<Exception>(() =>
            {
                mgr.DefineTransition("aaa", "aaa", (appContext, server) => true);
            });
        }

        [Test]
        public void OnlyOneTransitionPerState()
        {
            var mgr = new YolManager();
            mgr.DefineTransition("aaa", "bbb", (appContext, server) => true);
            Assert.Throws<Exception>(() =>
            {
                mgr.DefineTransition("aaa", "ccc", (appContext, server) => true);
            });
        }

        [Test]
        public void CannotContainTwoMoreHeads()
        {
            var mgr = new YolManager();
            mgr.DefineTransition(string.Empty, "aaa", (appContext, server) => true);
            mgr.DefineTransition("aaa", "bbb", (appContext, server) => true);
            mgr.DefineTransition("ccc", "ddd", (appContext, server) => true);
            Assert.Throws<Exception>(mgr.ValidateTransitions);
        }

        [Test]
        public void CannotContainLoops()
        {
            var mgr = new YolManager();
            mgr.DefineTransition(string.Empty, "aaa", (appContext, server) => true);
            mgr.DefineTransition("aaa", "bbb", (appContext, server) => true);
            mgr.DefineTransition("bbb", "ccc", (appContext, server) => true);
            mgr.DefineTransition("ccc", "aaa", (appContext, server) => true);
            Assert.Throws<Exception>(mgr.ValidateTransitions);
        }
    }
}
