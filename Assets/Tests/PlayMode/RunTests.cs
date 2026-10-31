using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DachshundDash.Tests
{
    /// <summary>Smoke tests in the real scenes: menu, a run that survives on invincibility, a run that ends in a
    /// collision and returns to the menu.</summary>
    public class RunTests
    {
        [SetUp]
        public void SetUp()
        {
            GameRoot.SeedOverride = 1234;
            GameRoot.CountdownStep = 0.1f;
            if (!Services.Profile.HasName) Services.Profile.TrySetName("Tester");
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.SeedOverride = null;
            GameRoot.CountdownStep = 1f;
            Time.timeScale = 1f;
        }

        static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuBuilds()
        {
            yield return Load("Menu");
            Assert.IsNotNull(Object.FindAnyObjectByType<MenuRoot>());
            Assert.IsNotNull(GameObject.Find("MenuUI"));
        }

        [UnityTest]
        public IEnumerator GodModeRunCollectsBonesAndKeepsGoing()
        {
            yield return Load("Game");
            var root = Object.FindAnyObjectByType<GameRoot>();
            while (root.Current == GameRoot.State.Countdown) yield return null;
            Assert.AreEqual(GameRoot.State.Running, root.Current);
            float start = root.Dog.X;
            float t = 0;
            int lane = 1, dir = 1;
            float nextSwitch = 1f;
            while (t < 25f)
            {
                root.Dog.GodMode = true;
                if (t > nextSwitch)       // weave through the lanes to pick up bones
                {
                    nextSwitch = t + 1.3f;
                    if (lane == 2) dir = -1; else if (lane == 0) dir = 1;
                    lane += dir;
                    if (dir < 0) root.Input.PressLeft(); else root.Input.PressRight();
                }
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(GameRoot.State.Running, root.Current, "the dog should still be running");
            Assert.Greater(root.Dog.X - start, 80f, "distance");          // 25 s at 4 -> 6.5 m/s
            Assert.Greater(root.Dog.Bones, 0, "bones");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CollisionEndsTheRunAndReturnsToMenu()
        {
            yield return Load("Game");
            var root = Object.FindAnyObjectByType<GameRoot>();
            float t = 0;
            while (root.Current != GameRoot.State.Result && t < 90f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(GameRoot.State.Result, root.Current, "a dog that never dodges must hit something");
            while (!root.CanContinue) yield return null;
            root.SendMessage("ContinueAfterResult");
            yield return null;
            yield return null;
            Assert.AreEqual("Menu", SceneManager.GetActiveScene().name);
        }
    }
}
