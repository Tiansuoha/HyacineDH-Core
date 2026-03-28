namespace HyacineCore.Server.Util
{
    public static class IConsole
    {
        public const string PrefixContent = "[HyacineCore]> "; // "[HyacineCore]> "; Before execute command
        private const string PromptColor = "\u001b[38;2;244;169;189m";
        private const string RedColor = "\u001b[38;2;255;0;0m";
        private const string ResetColor = "\u001b[0m";

        // coloured prefix
        public static string Prefix => $"{(IsCommandValid ? PromptColor : RedColor)}{PrefixContent}{ResetColor}";

        public static bool IsCommandValid { get; private set; } = true;
        private const int HistoryMaxCount = 10;

        public static List<char> Input { get; set; } = [];
        private static int CursorIndex { get; set; }
        private static readonly List<string> InputHistory = [];
        private static int HistoryIndex = -1;

        public static event Action<string>? OnConsoleExcuteCommand;

        public static bool IsConsoleAvailable
        {
            get
            {
                try
                {
                    _ = Environment.UserInteractive;
                    if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
                        return false;

                    _ = Console.BufferWidth;
                    _ = Console.CursorTop;
                    _ = Console.GetCursorPosition();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void InitConsole()
        {
            if (!IsConsoleAvailable) return;
            try { Console.Title = "HyacineCore Console"; } catch { }
        }

        public static int GetWidth(string str)
            => str.ToCharArray().Sum(EastAsianWidth.GetLength);

        public static void RedrawInput(List<char> input, bool hasPrefix = true)
            => RedrawInput(new string([.. input]), hasPrefix);

        public static void RedrawInput(string input, bool hasPrefix = true)
        {
            if (!IsConsoleAvailable) return;

            // check validity
            UpdateCommandValidity(input);

            var length = GetWidth(input);
            if (hasPrefix)
            {
                input = Prefix + input;
                length += GetWidth(PrefixContent);
            }

            var (left, _) = Console.GetCursorPosition();
            if (left > 0) Console.SetCursorPosition(0, Console.CursorTop);

            var padding = Console.BufferWidth - length;
            if (padding < 0) padding = 0;
            Console.Write(input + new string(' ', padding));

            // Prevent crash on narrow screens (Termux) when text wraps
            if (length < Console.BufferWidth)
            {
                Console.SetCursorPosition(length, Console.CursorTop);
            }
            
            CursorIndex = length - GetWidth(PrefixContent);
        }

        // check validity and update
        private static void UpdateCommandValidity(string input)
        {
            IsCommandValid = CheckCommandValid(input);
        }

        #region Handlers

        public static void HandleEnter()
        {
            var input = new string([.. Input]);
            if (string.IsNullOrWhiteSpace(input)) return;

            // New line
            Console.WriteLine();
            Input = [];
            CursorIndex = 0;
            if (InputHistory.Count >= HistoryMaxCount)
                InputHistory.RemoveAt(0);
            InputHistory.Add(input);
            HistoryIndex = InputHistory.Count;

            // Handle command
            if (input.StartsWith('/')) input = input[1..].Trim();
            OnConsoleExcuteCommand?.Invoke(input);

            // reset
            IsCommandValid = true;
        }

        public static void HandleBackspace()
        {
            if (CursorIndex <= 0) return;
            CursorIndex--;
            var targetWidth = GetWidth(Input[CursorIndex].ToString());
            Input.RemoveAt(CursorIndex);

            var (left, _) = Console.GetCursorPosition();
            Console.SetCursorPosition(left - targetWidth, Console.CursorTop);
            var remain = new string([.. Input.Skip(CursorIndex)]);
            Console.Write(remain + new string(' ', targetWidth));
            Console.SetCursorPosition(left - targetWidth, Console.CursorTop);

            // update
            var prev = IsCommandValid;
            UpdateCommandValidity(new string([.. Input]));

            if (IsCommandValid != prev)
            {
                RedrawInput(Input);
            }
        }

        public static void HandleUpArrow()
        {
            if (InputHistory.Count == 0) return;
            if (HistoryIndex <= 0) return;

            HistoryIndex--;
            var history = InputHistory[HistoryIndex];
            Input = [.. history];
            CursorIndex = Input.Count;

            // update
            UpdateCommandValidity(history);
            RedrawInput(Input);
        }

        public static void HandleDownArrow()
        {
            if (HistoryIndex >= InputHistory.Count) return;

            HistoryIndex++;
            if (HistoryIndex >= InputHistory.Count)
            {
                HistoryIndex = InputHistory.Count;
                Input = [];
                CursorIndex = 0;
                IsCommandValid = true;
            }
            else
            {
                var history = InputHistory[HistoryIndex];
                Input = [.. history];
                CursorIndex = Input.Count;
                // update
                UpdateCommandValidity(history);
            }
            RedrawInput(Input);
        }

        public static void HandleLeftArrow()
        {
            if (CursorIndex <= 0) return;

            var (left, _) = Console.GetCursorPosition();
            CursorIndex--;
            Console.SetCursorPosition(left - GetWidth(Input[CursorIndex].ToString()), Console.CursorTop);
        }

        public static void HandleRightArrow()
        {
            if (CursorIndex >= Input.Count) return;

            var (left, _) = Console.GetCursorPosition();
            CursorIndex++;
            Console.SetCursorPosition(left + GetWidth(Input[CursorIndex - 1].ToString()), Console.CursorTop);
        }

        public static void HandleInput(ConsoleKeyInfo keyInfo)
        {
            if (char.IsControl(keyInfo.KeyChar)) return;
            var newWidth = GetWidth(new string([.. Input])) + GetWidth(keyInfo.KeyChar.ToString());
            if (newWidth >= (Console.BufferWidth - GetWidth(PrefixContent))) return;
            HandleInput(keyInfo.KeyChar);
        }

        public static void HandleInput(char keyChar)
        {
            Input.Insert(CursorIndex, keyChar);
            CursorIndex++;

            var (left, _) = Console.GetCursorPosition();
            var newCursor = left + GetWidth(keyChar.ToString());
            if (newCursor > Console.BufferWidth - 1) newCursor = Console.BufferWidth - 1;

            Console.Write(new string([.. Input.Skip(CursorIndex - 1)]));
            Console.SetCursorPosition(newCursor, Console.CursorTop);

            // update
            var prev = IsCommandValid;
            UpdateCommandValidity(new string([.. Input]));

            if (IsCommandValid != prev)
            {
                RedrawInput(Input);
            }
        }

        #endregion

        public static string ListenConsole()
        {
            if (!IsConsoleAvailable) return string.Empty;

            while (true)
            {
                ConsoleKeyInfo keyInfo;
                try { keyInfo = Console.ReadKey(true); }
                catch (InvalidOperationException) { continue; }

                switch (keyInfo.Key)
                {
                    case ConsoleKey.Enter:
                        HandleEnter();
                        break;
                    case ConsoleKey.Backspace:
                        HandleBackspace();
                        break;
                    case ConsoleKey.LeftArrow:
                        HandleLeftArrow();
                        break;
                    case ConsoleKey.RightArrow:
                        HandleRightArrow();
                        break;
                    case ConsoleKey.UpArrow:
                        HandleUpArrow();
                        break;
                    case ConsoleKey.DownArrow:
                        HandleDownArrow();
                        break;
                    default:
                        HandleInput(keyInfo);
                        break;
                }
            }
        }

        private static bool CheckCommandValid(string input)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            var invalidChars = new[] { '@', '#', '$', '%', '&', '*' };
            return !invalidChars.Any(c => input.Contains(c));
        }
    }

    internal static class EastAsianWidth
    {
        public static int GetLength(char c)
        {
            return c <= 0x7F ? 1 : 2;
        }
    }
}
