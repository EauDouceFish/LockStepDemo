using System.IO;
using NUnit.Framework;
using Lockstep.Import.Sff;
using Lockstep.Import.Snd;
using Lockstep.Mugen.Parse;
using Lockstep.Tests.Mugen;

namespace Lockstep.Tests.Mugen.Import
{
    [TestFixture]
    public sealed class CharacterResourceImportTests
    {
        [Test]
        public void DefParser_ReadsRuntimeMetadataAndResourcePaths()
        {
            MCharacterDefinition definition = MDefParser.Parse(
                "[Info]\nname = Test\ndisplayname = \"Test Char\"\nlocalcoord = 640, 480\npal.defaults = 2, 4\n" +
                "[Files]\ncmd = test.cmd\ncns = test.cns\nsprite = test.sff\nanim = test.air\nsound = test.snd\n" +
                "st = test.cns\nst2 = extra.cns\npal1 = one.act\npal12 = twelve.act\n");

            Assert.That(definition.Name, Is.EqualTo("Test"));
            Assert.That(definition.DisplayName, Is.EqualTo("Test Char"));
            Assert.That(definition.LocalCoordWidth, Is.EqualTo(640));
            Assert.That(definition.LocalCoordHeight, Is.EqualTo(480));
            Assert.That(definition.PaletteDefaults, Is.EqualTo(new[] { 2, 4 }));
            Assert.That(definition.Files.Sound, Is.EqualTo("test.snd"));
            Assert.That(definition.Files.St, Is.EqualTo(new[] { "test.cns", "extra.cns" }));
            Assert.That(definition.Files.Palettes[12], Is.EqualTo("twelve.act"));
        }

        [Test]
        public void ActReader_ReversesElecbytePaletteOrder()
        {
            byte[] source = new byte[768];
            source[0] = 1; source[1] = 2; source[2] = 3;
            source[765] = 4; source[766] = 5; source[767] = 6;
            byte[] palette = ActPaletteReader.Read(source);

            Assert.That(palette[0], Is.EqualTo(4));
            Assert.That(palette[1], Is.EqualTo(5));
            Assert.That(palette[2], Is.EqualTo(6));
            Assert.That(palette[765], Is.EqualTo(1));
            Assert.That(palette[766], Is.EqualTo(2));
            Assert.That(palette[767], Is.EqualTo(3));
        }

        [TestCase("kfm")]
        [TestCase("Terrarian")]
        public void RealCharacter_SndAndActResourcesAreIndexable(string character)
        {
            string directory = TestAssets.CharDir(character);
            if (!Directory.Exists(directory)) { Assert.Ignore("MugenSource is unavailable."); }
            string defPath = MugenCharacterPackageTestLoader.PickMainDef(directory);
            MCharacterDefinition definition = MDefParser.Parse(File.ReadAllText(defPath));

            string sndPath = MugenCharacterPackageTestLoader.Resolve(directory, definition.Files.Sound);
            Assert.That(sndPath, Is.Not.Null, character + ": missing SND");
            SndFile snd = SndReader.Read(File.ReadAllBytes(sndPath));
            Assert.That(snd.Entries.Count, Is.GreaterThan(0), character + ": empty SND directory");
            Assert.That(SndReader.CopyWave(File.ReadAllBytes(sndPath), snd.Entries[0]), Is.Not.Empty);

            if (definition.Files.Palettes.TryGetValue(1, out string paletteFile))
            {
                string palettePath = MugenCharacterPackageTestLoader.Resolve(directory, paletteFile);
                Assert.That(palettePath, Is.Not.Null, character + ": missing ACT palette 1");
                Assert.That(ActPaletteReader.Read(File.ReadAllBytes(palettePath)).Length, Is.EqualTo(768));
            }
        }
    }
}
