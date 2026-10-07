using AgentClicker.Core;
using NUnit.Framework;

namespace AgentClicker.Tests
{
    public class TabTitleTests
    {
        [Test]
        public void OutsideAGameTheTabIsJustTheGamesName()
        {
            Assert.AreEqual("Agent Clicker", TabTitle.Compose(false, 2.2e16, true, true, true, true));
        }

        [Test]
        public void InAGameTheTabShowsTheCredits()
        {
            Assert.AreEqual("22.0Qa credits · Agent Clicker", TabTitle.Compose(true, 2.2e16, false, false, false, false));
            Assert.AreEqual("42 credits · Agent Clicker", TabTitle.Compose(true, 42.4, false, false, false, false));
        }

        [Test]
        public void WhatNeedsThePlayerGoesInFrontOneAtATime()
        {
            Assert.AreEqual("★ Model drop! · 961B credits · Agent Clicker", TabTitle.Compose(true, 9.61e11, true, true, true, true));
            Assert.AreEqual("☎ Phone ringing · 961B credits · Agent Clicker", TabTitle.Compose(true, 9.61e11, false, true, true, true));
            Assert.AreEqual("⚠ API outage · 961B credits · Agent Clicker", TabTitle.Compose(true, 9.61e11, false, false, true, true));
            Assert.AreEqual("5 PM: clock out? · 961B credits · Agent Clicker", TabTitle.Compose(true, 9.61e11, false, false, false, true));
        }
    }
}
