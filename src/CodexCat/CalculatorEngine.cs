using System;
using System.Globalization;

namespace CodexCat
{
    internal sealed class CalculatorEngine
    {
        private const int MaximumInputDigits = 15;
        private decimal accumulator;
        private decimal memory;
        private decimal lastOperand;
        private string pendingOperator;
        private string lastOperator;
        private bool replaceEntry;
        private bool error;

        public string Display { get; private set; }

        public string Status
        {
            get
            {
                string memoryMarker = memory == 0m ? string.Empty : "M  ";
                if (!string.IsNullOrEmpty(pendingOperator))
                {
                    return memoryMarker + Format(accumulator) + " " + pendingOperator;
                }
                return memoryMarker;
            }
        }

        public CalculatorEngine()
        {
            Display = "0";
            replaceEntry = true;
        }

        public void Press(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (key.Length == 1 && char.IsDigit(key[0]))
            {
                InputDigit(key);
                return;
            }

            switch (key)
            {
                case ".": InputDecimalPoint(); break;
                case "AC": ClearAll(); break;
                case "±": ToggleSign(); break;
                case "%": ApplyPercent(); break;
                case "+": BeginOperator("+"); break;
                case "−":
                case "-": BeginOperator("−"); break;
                case "×":
                case "*": BeginOperator("×"); break;
                case "÷":
                case "/": BeginOperator("÷"); break;
                case "=": ApplyEquals(); break;
                case "MC": memory = 0m; break;
                case "MR": RecallMemory(); break;
                case "M+": AddToMemory(false); break;
                case "M−":
                case "M-": AddToMemory(true); break;
            }
        }

        private void InputDigit(string digit)
        {
            RecoverFromError();
            if (replaceEntry)
            {
                Display = digit;
                replaceEntry = false;
                return;
            }

            if (CountDigits(Display) >= MaximumInputDigits) return;
            if (Display == "0") Display = digit;
            else if (Display == "-0") Display = "-" + digit;
            else Display += digit;
        }

        private void InputDecimalPoint()
        {
            RecoverFromError();
            if (replaceEntry)
            {
                Display = "0.";
                replaceEntry = false;
                return;
            }
            if (Display.IndexOf(".", StringComparison.Ordinal) < 0) Display += ".";
        }

        private void ToggleSign()
        {
            if (error || Display == "0") return;
            Display = Display.StartsWith("-", StringComparison.Ordinal) ? Display.Substring(1) : "-" + Display;
        }

        private void ApplyPercent()
        {
            if (error) return;
            try
            {
                Display = Format(ReadDisplay() / 100m);
                replaceEntry = true;
            }
            catch
            {
                SetError();
            }
        }

        private void BeginOperator(string operation)
        {
            if (error) return;
            try
            {
                if (!string.IsNullOrEmpty(pendingOperator) && !replaceEntry)
                {
                    if (!ApplyOperation(ReadDisplay())) return;
                }
                else if (string.IsNullOrEmpty(pendingOperator))
                {
                    accumulator = ReadDisplay();
                }
                pendingOperator = operation;
                replaceEntry = true;
                lastOperator = null;
            }
            catch
            {
                SetError();
            }
        }

        private void ApplyEquals()
        {
            if (error) return;
            try
            {
                if (!string.IsNullOrEmpty(pendingOperator))
                {
                    decimal right = replaceEntry ? accumulator : ReadDisplay();
                    lastOperator = pendingOperator;
                    lastOperand = right;
                    if (!ApplyOperation(right)) return;
                    pendingOperator = null;
                    replaceEntry = true;
                    return;
                }

                if (!string.IsNullOrEmpty(lastOperator))
                {
                    accumulator = ReadDisplay();
                    pendingOperator = lastOperator;
                    if (!ApplyOperation(lastOperand)) return;
                    pendingOperator = null;
                    replaceEntry = true;
                }
            }
            catch
            {
                SetError();
            }
        }

        private bool ApplyOperation(decimal right)
        {
            try
            {
                switch (pendingOperator)
                {
                    case "+": accumulator += right; break;
                    case "−": accumulator -= right; break;
                    case "×": accumulator *= right; break;
                    case "÷":
                        if (right == 0m)
                        {
                            SetError();
                            return false;
                        }
                        accumulator /= right;
                        break;
                    default: accumulator = right; break;
                }
                Display = Format(accumulator);
                return true;
            }
            catch
            {
                SetError();
                return false;
            }
        }

        private void RecallMemory()
        {
            if (error) RecoverFromError();
            Display = Format(memory);
            replaceEntry = true;
        }

        private void AddToMemory(bool subtract)
        {
            if (error) return;
            try
            {
                decimal current = ReadDisplay();
                memory = subtract ? memory - current : memory + current;
                replaceEntry = true;
            }
            catch
            {
                SetError();
            }
        }

        private void ClearAll()
        {
            accumulator = 0m;
            lastOperand = 0m;
            pendingOperator = null;
            lastOperator = null;
            error = false;
            Display = "0";
            replaceEntry = true;
        }

        private void RecoverFromError()
        {
            if (!error) return;
            ClearAll();
        }

        private void SetError()
        {
            error = true;
            pendingOperator = null;
            lastOperator = null;
            Display = "错误";
            replaceEntry = true;
        }

        private decimal ReadDisplay()
        {
            return decimal.Parse(Display, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string Format(decimal value)
        {
            if (value == 0m) return "0";
            string normal = value.ToString("0.###############", CultureInfo.InvariantCulture);
            if (normal.Length <= 18) return normal;
            return value.ToString("0.########E+0", CultureInfo.InvariantCulture);
        }

        private static int CountDigits(string value)
        {
            int count = 0;
            foreach (char character in value)
            {
                if (char.IsDigit(character)) count++;
            }
            return count;
        }
    }
}
