using System.Collections.Generic;

namespace Lockstep.Mugen.Command
{
    public static class MInputDisplayFormatter
    {
        public static string Format(MInput input)
        {
            if (input == MInput.None)
            {
                return "None";
            }

            List<string> names = new List<string>();
            Add(names, input, MInput.Up, "U");
            Add(names, input, MInput.Down, "D");
            Add(names, input, MInput.Left, "L");
            Add(names, input, MInput.Right, "R");
            Add(names, input, MInput.X, "x");
            Add(names, input, MInput.Y, "y");
            Add(names, input, MInput.Z, "z");
            Add(names, input, MInput.A, "a");
            Add(names, input, MInput.B, "b");
            Add(names, input, MInput.C, "c");
            Add(names, input, MInput.S, "s");
            return string.Join(" + ", names.ToArray());
        }

        static void Add(List<string> names, MInput input, MInput bit, string label)
        {
            if ((input & bit) != 0)
            {
                names.Add(label);
            }
        }
    }
}
