using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class GameSettingsTests
    {
        [SetUp] public void Clean() => GameSettings.ClearStorage();

        [TearDown]
        public void Restore()
        {
            GameSettings.ClearStorage();
            GameSettings.ReloadFromStorage();
        }

        [Test]
        public void DefaultsAreSensible()
        {
            Assert.AreEqual(1f, GameSettings.MasterVolume);
            Assert.AreEqual(GameSettings.DefaultMusic, GameSettings.MusicVolume);
            Assert.AreEqual(1f, GameSettings.SfxVolume);
            Assert.AreEqual(UiSize.Normal, GameSettings.UiSize);
            Assert.AreEqual(ShakeLevel.Full, GameSettings.CameraShake);
            Assert.AreEqual(1f, GameSettings.LookSpeed);
            Assert.IsTrue(GameSettings.Rumble);
            Assert.IsTrue(GameSettings.Tips);
        }

        [Test]
        public void ValuesAreClamped()
        {
            GameSettings.MasterVolume = 3f;
            GameSettings.MusicVolume = -1f;
            GameSettings.LookSpeed = 10f;
            Assert.AreEqual(1f, GameSettings.MasterVolume);
            Assert.AreEqual(0f, GameSettings.MusicVolume);
            Assert.AreEqual(GameSettings.MaxLookSpeed, GameSettings.LookSpeed);
            GameSettings.LookSpeed = 0f;
            Assert.AreEqual(GameSettings.MinLookSpeed, GameSettings.LookSpeed);
            GameSettings.UiSize = (UiSize)42;
            Assert.AreEqual(UiSize.Small, GameSettings.UiSize, "An unknown enum value falls back to the default of the enum.");
        }

        [Test]
        public void ValuesSurviveReloadingFromStorage()
        {
            GameSettings.MasterVolume = .35f;
            GameSettings.SfxVolume = .6f;
            GameSettings.UiSize = UiSize.Large;
            GameSettings.CameraShake = ShakeLevel.Low;
            GameSettings.LookSpeed = 1.5f;
            GameSettings.Rumble = false;
            GameSettings.Tips = false;

            GameSettings.ReloadFromStorage();

            Assert.AreEqual(.35f, GameSettings.MasterVolume, .0001f);
            Assert.AreEqual(.6f, GameSettings.SfxVolume, .0001f);
            Assert.AreEqual(UiSize.Large, GameSettings.UiSize);
            Assert.AreEqual(ShakeLevel.Low, GameSettings.CameraShake);
            Assert.AreEqual(1.5f, GameSettings.LookSpeed, .0001f);
            Assert.IsFalse(GameSettings.Rumble);
            Assert.IsFalse(GameSettings.Tips);
        }

        [Test]
        public void CorruptStoredValuesAreIgnored()
        {
            PlayerPrefs.SetFloat(GameSettings.KeyPrefix + "master", 9f);
            PlayerPrefs.SetInt(GameSettings.KeyPrefix + "uisize", 77);
            GameSettings.ReloadFromStorage();
            Assert.AreEqual(1f, GameSettings.MasterVolume);
            Assert.AreEqual(UiSize.Small, GameSettings.UiSize);
        }

        [Test]
        public void ChangedFiresOnlyForRealChanges()
        {
            int calls = 0;
            void Count() => calls++;
            GameSettings.Changed += Count;
            try
            {
                GameSettings.Rumble = true;          // already true
                Assert.AreEqual(0, calls);
                GameSettings.Rumble = false;
                Assert.AreEqual(1, calls);
                GameSettings.ResetToDefaults();
                Assert.AreEqual(2, calls, "A reset is announced once.");
                Assert.IsTrue(GameSettings.Rumble);
            }
            finally { GameSettings.Changed -= Count; }
        }

        [Test]
        public void ScalesFollowTheirLevels()
        {
            Assert.Less(GameSettings.UiScaleFor(UiSize.Small), GameSettings.UiScaleFor(UiSize.Normal));
            Assert.AreEqual(1f, GameSettings.UiScaleFor(UiSize.Normal));
            Assert.Greater(GameSettings.UiScaleFor(UiSize.Large), 1f);
            Assert.AreEqual(0f, GameSettings.ShakeScaleFor(ShakeLevel.Off));
            Assert.Greater(GameSettings.ShakeScaleFor(ShakeLevel.Low), 0f);
            Assert.AreEqual(1f, GameSettings.ShakeScaleFor(ShakeLevel.Full));
        }

        [Test]
        public void VolumesMultiplyAndMuteSilencesEverything()
        {
            bool muted = FeedbackSettings.Muted;
            try
            {
                FeedbackSettings.Muted = false;
                GameSettings.MasterVolume = .5f;
                GameSettings.SfxVolume = .5f;
                GameSettings.MusicVolume = .8f;
                Assert.AreEqual(.25f, FeedbackSettings.SfxGain, .0001f);
                Assert.AreEqual(.4f, FeedbackSettings.MusicGain, .0001f);
                FeedbackSettings.Muted = true;
                Assert.AreEqual(0f, FeedbackSettings.SfxGain);
                Assert.AreEqual(0f, FeedbackSettings.MusicGain);
            }
            finally { FeedbackSettings.Muted = muted; }
        }

        [Test]
        public void RumbleIsANoOpWithoutAGamepadOrWhenOff()
        {
            Assert.DoesNotThrow(() => Haptics.Pulse(1, 1, .2f));
            GameSettings.Rumble = false;
            Assert.DoesNotThrow(() => Haptics.Pulse(1, 1, .2f));
        }
    }
}
