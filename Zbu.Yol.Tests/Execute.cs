using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Zbu.Yol.Tests
{
    [TestFixture]
    public class Execute
    {
        [Test]
        public void Simple()
        {
            var mgr = new YolManager();
            mgr.DefineTransition(string.Empty, "aaa", () => true);
            mgr.ExecuteInternal();
            Assert.AreEqual("aaa", mgr.State);
        }

        [Test]
        public void Multiple()
        {
            var mgr = new YolManager();
            mgr.DefineTransition(string.Empty, "aaa", () => true);
            mgr.DefineTransition("aaa", "bbb", () => true);
            mgr.DefineTransition("bbb", "ccc", () => true);
            mgr.ExecuteInternal();
            Assert.AreEqual("ccc", mgr.State);
        }
    }
}
