using System;

namespace CodexCat
{
    internal static class CalculatorVerifier
    {
        public static void Run()
        {
            AssertSequence("15", "1", "2", "+", "3", "=");
            AssertSequence("3", "1", ".", "5", "×", "2", "=");
            AssertSequence("-7", "7", "±");
            AssertSequence("0.25", "2", "5", "%");
            AssertSequence("错误", "9", "÷", "0", "=");
            AssertSequence("8", "2", "+", "3", "=", "=");

            CalculatorEngine memory = new CalculatorEngine();
            Press(memory, "5", "M+", "AC", "2", "M+", "MR");
            AssertDisplay(memory, "7", "M+ / MR memory sequence");
            Press(memory, "M−", "MR");
            AssertDisplay(memory, "0", "M− memory sequence");

            CalculatorPlacementVerifier.Run();
        }

        private static void AssertSequence(string expected, params string[] keys)
        {
            CalculatorEngine engine = new CalculatorEngine();
            Press(engine, keys);
            AssertDisplay(engine, expected, string.Join(" ", keys));
        }

        private static void Press(CalculatorEngine engine, params string[] keys)
        {
            foreach (string key in keys) engine.Press(key);
        }

        private static void AssertDisplay(CalculatorEngine engine, string expected, string sequence)
        {
            if (!string.Equals(engine.Display, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Calculator verification failed for " + sequence + ". Expected " + expected + ", got " + engine.Display + ".");
            }
        }
    }
}
