using NUnit.Framework;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class PresentationEventTests
    {
        const string Cns = @"
[Statedef 0]
type = S
movetype = I
physics = N

[State 0, sound]
type = PlaySnd
trigger1 = time = 0
value = S3, 7
channel = 2
volumescale = 80
pan = 12
freqmul = 2
";

        [Test]
        public void PlaySnd_EmitsFrameEventWithoutOwningAudioDevice()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, null, null, "sound");
            MChar character = MCharLoader.SpawnChar(data, 1);
            MBattleEngine engine = new MBattleEngine();
            engine.Add(character, data);
            engine.Tick(new[] { MInput.None });

            Assert.That(engine.World.Events.Sounds.Count, Is.EqualTo(1));
            MSoundEvent sound = engine.World.Events.Sounds[0];
            Assert.That(sound.Type, Is.EqualTo(MSoundEventType.Play));
            Assert.That(sound.CommonBank, Is.False);
            Assert.That(sound.Group, Is.EqualTo(3));
            Assert.That(sound.Number, Is.EqualTo(7));
            Assert.That(sound.Channel, Is.EqualTo(2));
            Assert.That(sound.VolumeScale, Is.EqualTo(80));
        }

        [Test]
        public void FrameEvents_AreClearedAtNextTick()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, null, null, "sound");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 1), data);
            engine.Tick(new[] { MInput.None });
            engine.Tick(new[] { MInput.None });

            Assert.That(engine.World.Events.Frame, Is.EqualTo(1));
            Assert.That(engine.World.Events.Sounds, Is.Empty);
        }
    }
}
