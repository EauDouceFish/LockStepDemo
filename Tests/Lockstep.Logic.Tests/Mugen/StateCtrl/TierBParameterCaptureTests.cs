// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go Tier B non-entity StateController parameter blocks.
// Tests parser-side field capture before runtime MChar fields are available.
using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class TierBParameterCaptureTests
    {
        static FFloat Fixed(int value)
        {
            return FFloat.FromInt(value);
        }

        static TController FirstController<TController>(string cnsText) where TController : MStateController
        {
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cnsText);
            return (TController)states[100].Controllers[0];
        }

        [Test]
        public void Explod_CapturesCorePalFxAfterImageAndInterpolationParameters()
        {
            string text = @"
[Statedef 100]
[State 100, explod]
type = Explod
trigger1 = 1
anim = 1000
ownpal = 1
remappal = 1, 2
id = 40
facing = -1
vfacing = 1
pos = 10, 20, 30
random = 4, 6, 8
postype = 2
vel = 1, 2, 3
friction = 0.9, 0.8, 0.7
accel = 4, 5, 6
scale = 2, 3
bindtime = 9
removetime = 10
supermove = 1
supermovetime = 11
pausemovetime = 12
sprpriority = 13
layerno = 1
under = 1
ontop = 0
shadow = 1, 2, 3
removeongethit = 1
removeonchangestate = 1
hidewithbars = 1
trans = 128, 64, 1
animelem = 3
animelemtime = 4
animfreeze = 1
angle = 15
yangle = 16
xangle = 17
xshear = 0.25
projection = 1
focallength = 400
ignorehitpause = 1
bindid = 88
space = 1
window = 1, 2, 3, 4
interpolation.time = 5
interpolation.animelem = 6
interpolation.pos = 7, 8, 9
interpolation.scale = 1.5, 2.5
interpolation.angle = 22
interpolation.alpha = 255, 0, 1
interpolation.focallength = 450
interpolation.xshear = 0.5
interpolation.pfx.mul = 200, 201, 202
interpolation.pfx.add = 1, 2, 3
interpolation.pfx.color = 128
interpolation.pfx.hue = 64
animplayerno = 2
spriteplayerno = 3
syncparams = 1
synclayer = 4
syncid = 5
shader = 99
shaderparam = 1, 2, 3
time = 14
length = 15
timegap = 16
framegap = 17
palcolor = 128
palhue = 64
palinvertall = 1
palinvertblend = 1
palbright = 1, 2, 3
palcontrast = 4, 5, 6
palpostbright = 7, 8, 9
paladd = 10, 11, 12
palmul = 0.1, 0.2, 0.3
add = 13, 14, 15
mul = 16, 17, 18
sinadd = 19, 20, 21, 22
sinmul = 23, 24, 25, 26
sincolor = 27, 28
sinhue = 29, 30
invertall = 1
invertblend = 1
hue = 31
color = 32
";

            ExplodController controller = FirstController<ExplodController>(text);
            MChar character = new MChar();

            Assert.That(controller.Anim[0].Run(character).ToI(), Is.EqualTo(1000));
            Assert.That(controller.OwnPal.Run(character).ToB(), Is.True);
            Assert.That(controller.RemapPal[1].Run(character).ToI(), Is.EqualTo(2));
            Assert.That(controller.Position[2].Run(character).ToI(), Is.EqualTo(30));
            Assert.That(controller.Velocity[1].Run(character).ToI(), Is.EqualTo(2));
            Assert.That(controller.Friction[0].Run(character).ToF().Raw, Is.EqualTo((Fixed(9) / Fixed(10)).Raw));
            Assert.That(controller.Trans[2].Run(character).ToI(), Is.EqualTo(1));
            Assert.That(controller.Interpolation.Position[2].Run(character).ToI(), Is.EqualTo(9));
            Assert.That(controller.Interpolation.PalFXMul[2].Run(character).ToI(), Is.EqualTo(202));
            Assert.That(controller.AfterImage.Time.Run(character).ToI(), Is.EqualTo(14));
            Assert.That(controller.AfterImage.PalMul[2].Run(character).ToF().Raw, Is.EqualTo((Fixed(3) / Fixed(10)).Raw));
            Assert.That(controller.PalFX.SinHue[1].Run(character).ToI(), Is.EqualTo(30));
            Assert.That(controller.ShaderParam[2].Run(character).ToI(), Is.EqualTo(3));
        }

        [Test]
        public void PauseSuperPauseHitOverrideAndScreenParams_AreStronglyCaptured()
        {
            string text = @"
[Statedef 100]
[State 100, super]
type = SuperPause
trigger1 = 1
time = 30
movetime = 5
pausebg = 0
endcmdbuftime = 2
darken = 1
brightness = 128
anim = 100, 200
pos = 1, 2, 3
p2defmul = 0.5
poweradd = -100
unhittable = 0
sound = 1, 2, 3
";

            SuperPauseController controller = FirstController<SuperPauseController>(text);
            MChar character = new MChar();

            Assert.That(controller.Time.Run(character).ToI(), Is.EqualTo(30));
            Assert.That(controller.MoveTime.Run(character).ToI(), Is.EqualTo(5));
            Assert.That(controller.PauseBg.Run(character).ToB(), Is.False);
            Assert.That(controller.EndCmdBufTime.Run(character).ToI(), Is.EqualTo(2));
            Assert.That(controller.Brightness.Run(character).ToI(), Is.EqualTo(128));
            Assert.That(controller.Anim[1].Run(character).ToI(), Is.EqualTo(200));
            Assert.That(controller.Position[2].Run(character).ToI(), Is.EqualTo(3));
            Assert.That(controller.P2DefMul.Run(character).ToF().Raw, Is.EqualTo((Fixed(1) / Fixed(2)).Raw));
            Assert.That(controller.Unhittable.Run(character).ToB(), Is.False);
            Assert.That(controller.Sound[2].Run(character).ToI(), Is.EqualTo(3));
        }

        [Test]
        public void ReversalDef_CapturesReversalAndHitDefTemplateFields()
        {
            string text = @"
[Statedef 100]
[State 100, reversal]
type = ReversalDef
trigger1 = 1
attr = SCA, AA
guardflag = HA
guardflag.not = L
damage = 40, 5
pausetime = 3, 4
ground.velocity = -6, -2
fall = 1
";

            ReversalDefController controller = FirstController<ReversalDefController>(text);

            Assert.That(controller.Attr, Is.Not.EqualTo(0));
            Assert.That(controller.GuardFlag, Is.Not.EqualTo(0));
            Assert.That(controller.GuardFlagNot, Is.Not.EqualTo(0));
            Assert.That(controller.Template.HitDamage, Is.EqualTo(40));
            Assert.That(controller.Template.P1PauseTime, Is.EqualTo(3));
            Assert.That(controller.Template.P2PauseTime, Is.EqualTo(4));
            Assert.That(controller.Template.GroundVelX.Raw, Is.EqualTo(FFloat.FromInt(-6).Raw));
            Assert.IsTrue(controller.Template.Fall);
        }

        [Test]
        public void PalFxAfterImageTextAndColdControllers_CaptureAllNamedParameters()
        {
            string text = @"
[Statedef 100]
[State 100, pal]
type = PalFX
trigger1 = 1
time = 6
color = 128
add = 1, 2, 3
mul = 4, 5, 6
sinadd = 7, 8, 9, 10
sinmul = 11, 12, 13, 14
sincolor = 15, 16
sinhue = 17, 18
invertall = 1
invertblend = 2
hue = 19
";
            PalFXController palFX = FirstController<PalFXController>(text);
            MChar character = new MChar();
            Assert.That(palFX.PalFX.Time.Run(character).ToI(), Is.EqualTo(6));
            Assert.That(palFX.PalFX.SinMul[3].Run(character).ToI(), Is.EqualTo(14));
            Assert.That(palFX.PalFX.Hue.Run(character).ToI(), Is.EqualTo(19));

            string textControllerText = @"
[Statedef 100]
[State 100, text]
type = Text
trigger1 = 1
removetime = 60
layerno = 1
params = 1, 2
font = 0, 1
localcoord = 320, 240
bank = 2
align = 1
textspacing = 3, 4
textdelay = 5
text = 6
pos = 7, 8
velocity = 9, 10
maxdist = 11, 12
friction = 0.9, 0.8
accel = 13, 14
angle = 15
xangle = 16
yangle = 17
projection = 1
focallength = 18
scale = 2, 3
color = 255, 128, 64, 32
xshear = 0.5
hidewithbars = 1
id = 99
";
            TextController textController = FirstController<TextController>(textControllerText);
            Assert.That(textController.Removetime.Run(character).ToI(), Is.EqualTo(60));
            Assert.That(textController.Params[1].Run(character).ToI(), Is.EqualTo(2));
            Assert.That(textController.Color[3].Run(character).ToI(), Is.EqualTo(32));
            Assert.That(textController.Id.Run(character).ToI(), Is.EqualTo(99));
        }
    }
}
