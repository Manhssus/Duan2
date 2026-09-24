using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace Universal_x86_Tuning_Utility.Services
{
    /// <summary>
    /// Bộ hỗ trợ gõ tiếng Việt Telex thông minh cho WPF TextBox.
    /// Giúp người dùng gõ tiếng Việt mượt mà ngay cả khi UniKey/EVKey bị chặn bởi Windows UIPI (quyền Admin),
    /// đồng thời tương thích hoàn hảo nếu người dùng đang dùng UniKey/EVKey chạy quyền Admin.
    /// </summary>
    public static class VietnameseInputHelper
    {
        public static bool IsEnabled { get; set; } = true;

        // Bảng nguyên âm cơ sở và các biến thể mũ/móc
        private static readonly Dictionary<char, char> AccentMap = new Dictionary<char, char>
        {
            {'a', 'â'}, {'A', 'Â'},
            {'e', 'ê'}, {'E', 'Ê'},
            {'o', 'ô'}, {'O', 'Ô'},
            {'d', 'đ'}, {'D', 'Đ'}
        };

        // Bảng dấu thanh cho từng nguyên âm: [Không dấu, Sắc (s), Huyền (f), Hỏi (r), Ngã (x), Nặng (j)]
        private static readonly char[][] ToneTable = new char[][]
        {
            new char[] { 'a', 'á', 'à', 'ả', 'ã', 'ạ' },
            new char[] { 'A', 'Á', 'À', 'Ả', 'Ã', 'Ạ' },
            new char[] { 'ă', 'ắ', 'ằ', 'ẳ', 'ẵ', 'ặ' },
            new char[] { 'Ă', 'Ắ', 'Ằ', 'Ẳ', 'Ẵ', 'Ặ' },
            new char[] { 'â', 'ấ', 'ầ', 'ẩ', 'ẫ', 'ậ' },
            new char[] { 'Â', 'Ấ', 'Ầ', 'Ẩ', 'Ẫ', 'Ậ' },
            new char[] { 'e', 'é', 'è', 'ẻ', 'ẽ', 'ẹ' },
            new char[] { 'E', 'É', 'È', 'Ẻ', 'Ẽ', 'Ẹ' },
            new char[] { 'ê', 'ế', 'ề', 'ể', 'ễ', 'ệ' },
            new char[] { 'Ê', 'Ế', 'Ề', 'Ể', 'Ễ', 'Ệ' },
            new char[] { 'i', 'í', 'ì', 'ỉ', 'ĩ', 'ị' },
            new char[] { 'I', 'Í', 'Ì', 'Ỉ', 'Ĩ', 'Ị' },
            new char[] { 'o', 'ó', 'ò', 'ỏ', 'õ', 'ọ' },
            new char[] { 'O', 'Ó', 'Ò', 'Ỏ', 'Õ', 'Ọ' },
            new char[] { 'ô', 'ố', 'ồ', 'ổ', 'ỗ', 'ộ' },
            new char[] { 'Ô', 'Ố', 'Ồ', 'Ổ', 'Ỗ', 'Ộ' },
            new char[] { 'ơ', 'ớ', 'ờ', 'ở', 'ỡ', 'ợ' },
            new char[] { 'Ơ', 'Ớ', 'Ờ', 'Ở', 'Ỡ', 'Ợ' },
            new char[] { 'u', 'ú', 'ù', 'ủ', 'ũ', 'ụ' },
            new char[] { 'U', 'Ú', 'Ù', 'Ủ', 'Ũ', 'Ụ' },
            new char[] { 'ư', 'ứ', 'ừ', 'ử', 'ữ', 'ự' },
            new char[] { 'Ư', 'Ứ', 'Ừ', 'Ử', 'Ữ', 'Ự' },
            new char[] { 'y', 'ý', 'ỳ', 'ỷ', 'ỹ', 'ỵ' },
            new char[] { 'Y', 'Ý', 'Ỳ', 'Ỷ', 'Ỹ', 'Ỵ' }
        };

        /// <summary>
        /// Gắn bộ hỗ trợ Telex vào TextBox
        /// </summary>
        public static void Attach(TextBox textBox)
        {
            if (textBox == null) return;
            textBox.PreviewTextInput -= TextBox_PreviewTextInput;
            textBox.PreviewTextInput += TextBox_PreviewTextInput;
        }

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsEnabled) return;
            if (sender is not TextBox textBox) return;
            if (string.IsNullOrEmpty(e.Text)) return;

            char ch = e.Text[0];

            // Chỉ xử lý các ký tự Telex ASCII tiềm năng
            if (!IsTelexKey(ch)) return;

            string currentText = textBox.Text;
            int caretIndex = textBox.CaretIndex;

            // Tìm từ hiện tại tính đến vị trí con trỏ
            int wordStartIndex = caretIndex - 1;
            while (wordStartIndex >= 0 && !char.IsWhiteSpace(currentText[wordStartIndex]) && !char.IsPunctuation(currentText[wordStartIndex]))
            {
                wordStartIndex--;
            }
            wordStartIndex++; // Đầu từ

            if (caretIndex < wordStartIndex) return;

            string wordBeforeCaret = currentText.Substring(wordStartIndex, caretIndex - wordStartIndex);

            // Thử biến đổi từ với ký tự mới
            string transformed = TransformWord(wordBeforeCaret, ch);
            if (transformed != null && transformed != (wordBeforeCaret + ch))
            {
                // Có sự biến đổi Telex! Thay thế từ hiện tại trong TextBox
                e.Handled = true;

                string prefix = currentText.Substring(0, wordStartIndex);
                string suffix = currentText.Substring(caretIndex);

                textBox.Text = prefix + transformed + suffix;
                textBox.CaretIndex = wordStartIndex + transformed.Length;
            }
        }

        private static bool IsTelexKey(char c)
        {
            char lower = char.ToLowerInvariant(c);
            return lower == 'a' || lower == 'e' || lower == 'o' || lower == 'w' ||
                   lower == 'd' || lower == 's' || lower == 'f' || lower == 'r' ||
                   lower == 'x' || lower == 'j';
        }

        /// <summary>
        /// Biến đổi một từ tiếng Việt khi thêm ký tự ch theo quy tắc Telex
        /// </summary>
        public static string TransformWord(string word, char nextChar)
        {
            if (string.IsNullOrEmpty(word))
            {
                if (char.ToLowerInvariant(nextChar) == 'w')
                {
                    return char.IsUpper(nextChar) ? "Ư" : "ư";
                }
                return null;
            }

            char lowerKey = char.ToLowerInvariant(nextChar);
            char lastChar = word[word.Length - 1];
            char lowerLast = char.ToLowerInvariant(lastChar);

            // 1. Phím 'd' -> 'đ' / 'dd' -> 'd'
            if (lowerKey == 'd')
            {
                if (lowerLast == 'd')
                {
                    bool isUpper = char.IsUpper(lastChar);
                    return word.Substring(0, word.Length - 1) + (isUpper ? "Đ" : "đ");
                }
                else if (lowerLast == 'đ')
                {
                    bool isUpper = char.IsUpper(lastChar);
                    return word.Substring(0, word.Length - 1) + (isUpper ? "Dd" : "dd");
                }
            }

            // 2. Phím 'a', 'e', 'o' tạo mũ: aa -> â, ee -> ê, oo -> ô
            if (lowerKey == 'a' && lowerLast == 'a')
            {
                return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Â" : "â");
            }
            if (lowerKey == 'a' && lowerLast == 'â')
            {
                return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Aa" : "aa");
            }

            if (lowerKey == 'e' && lowerLast == 'e')
            {
                return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Ê" : "ê");
            }
            if (lowerKey == 'e' && lowerLast == 'ê')
            {
                return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Ee" : "ee");
            }

            if (lowerKey == 'o' && lowerLast == 'o')
            {
                return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Ô" : "ô");
            }
            if (lowerKey == 'o' && lowerLast == 'ô')
            {
                return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Oo" : "oo");
            }

            // 3. Phím 'w' tạo móc hoặc chuyển đổi ư, ơ, ă
            if (lowerKey == 'w')
            {
                // Nếu ký tự trước là 'a' -> 'ă'
                if (lowerLast == 'a')
                {
                    return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Ă" : "ă");
                }
                if (lowerLast == 'ă')
                {
                    return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Aw" : "aw");
                }

                // Nếu ký tự trước là 'o' -> 'ơ'
                if (lowerLast == 'o')
                {
                    return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Ơ" : "ơ");
                }
                if (lowerLast == 'ơ')
                {
                    return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Ow" : "ow");
                }

                // Nếu ký tự trước là 'u' -> 'ư'
                if (lowerLast == 'u')
                {
                    // Nếu là 'uo' -> 'ươ'
                    if (word.Length >= 2 && char.ToLowerInvariant(word[word.Length - 2]) == 'u' && lowerLast == 'o')
                    {
                        char uChar = word[word.Length - 2];
                        char oChar = word[word.Length - 1];
                        return word.Substring(0, word.Length - 2) +
                               (char.IsUpper(uChar) ? "Ư" : "ư") +
                               (char.IsUpper(oChar) ? "Ơ" : "ơ");
                    }
                    return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Ư" : "ư");
                }
                if (lowerLast == 'ư')
                {
                    return word.Substring(0, word.Length - 1) + (char.IsUpper(lastChar) ? "Uw" : "uw");
                }

                // Nếu trong từ có 'uo' chưa móc -> 'ươ'
                int uoIndex = FindSubstringCaseInsensitive(word, "uo");
                if (uoIndex >= 0)
                {
                    char uChar = word[uoIndex];
                    char oChar = word[uoIndex + 1];
                    return word.Substring(0, uoIndex) +
                           (char.IsUpper(uChar) ? "Ư" : "ư") +
                           (char.IsUpper(oChar) ? "Ơ" : "ơ") +
                           word.Substring(uoIndex + 2);
                }

                // 'w' đơn lẻ đứng sau phụ âm hoặc nguyên âm khác -> 'ư'
                return word + (char.IsUpper(nextChar) ? "Ư" : "ư");
            }

            // 4. Dấu thanh: s (sắc), f (huyền), r (hỏi), x (ngã), j (nặng)
            int toneIndex = GetToneIndexFromKey(lowerKey);
            if (toneIndex > 0)
            {
                string toned = ApplyToneToWord(word, toneIndex, lowerKey);
                if (toned != null && toned != word)
                {
                    return toned;
                }
            }

            return null;
        }

        private static int FindSubstringCaseInsensitive(string source, string target)
        {
            return source.IndexOf(target, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetToneIndexFromKey(char key)
        {
            switch (key)
            {
                case 's': return 1; // Sắc
                case 'f': return 2; // Huyền
                case 'r': return 3; // Hỏi
                case 'x': return 4; // Ngã
                case 'j': return 5; // Nặng
                default: return 0;
            }
        }

        /// <summary>
        /// Đặt dấu thanh vào nguyên âm thích hợp nhất trong từ theo chuẩn tiếng Việt
        /// </summary>
        private static string ApplyToneToWord(string word, int toneIndex, char toneKey)
        {
            // Tìm các vị trí nguyên âm trong từ
            List<int> vowelPositions = new List<int>();
            for (int i = 0; i < word.Length; i++)
            {
                if (IsVowel(word[i]))
                {
                    vowelPositions.Add(i);
                }
            }

            if (vowelPositions.Count == 0) return null;

            // Kiểm tra xem từ đã có dấu thanh chưa
            int currentTone = 0;
            int currentTonedVowelPos = -1;
            for (int i = 0; i < vowelPositions.Count; i++)
            {
                int pos = vowelPositions[i];
                int t = GetCurrentTone(word[pos]);
                if (t > 0)
                {
                    currentTone = t;
                    currentTonedVowelPos = pos;
                    break;
                }
            }

            // Nếu gõ lại đúng dấu thanh đó -> Xóa dấu thanh (undo tone)
            if (currentTone == toneIndex && currentTonedVowelPos >= 0)
            {
                char baseChar = RemoveTone(word[currentTonedVowelPos]);
                StringBuilder sbUndo = new StringBuilder(word);
                sbUndo[currentTonedVowelPos] = baseChar;
                return sbUndo.ToString() + toneKey;
            }

            // Chọn nguyên âm nhận dấu
            int targetPos = SelectVowelForTone(word, vowelPositions);
            if (targetPos < 0) return null;

            // Đặt dấu thanh mới
            StringBuilder sb = new StringBuilder(word);

            // Nếu nguyên âm khác đang có dấu, gỡ bỏ dấu đó
            if (currentTonedVowelPos >= 0 && currentTonedVowelPos != targetPos)
            {
                sb[currentTonedVowelPos] = RemoveTone(sb[currentTonedVowelPos]);
            }

            char targetChar = sb[targetPos];
            char tonedChar = ApplyToneToChar(targetChar, toneIndex);
            if (tonedChar == targetChar) return null;

            sb[targetPos] = tonedChar;
            return sb.ToString();
        }

        private static int SelectVowelForTone(string word, List<int> vowelPositions)
        {
            int count = vowelPositions.Count;
            if (count == 1) return vowelPositions[0];

            int lastVowelPos = vowelPositions[count - 1];
            bool hasFinalConsonant = (lastVowelPos < word.Length - 1);

            // Nếu có 2 nguyên âm
            if (count == 2)
            {
                char v1 = char.ToLowerInvariant(word[vowelPositions[0]]);
                char v2 = char.ToLowerInvariant(word[vowelPositions[1]]);

                // Nếu có phụ âm cuối: dấu đặt ở nguyên âm thứ 2 (tiếng, muốn, được, toán,...)
                if (hasFinalConsonant)
                {
                    return vowelPositions[1];
                }

                // Không có phụ âm cuối:
                // oa, oe, uy: dấu ở nguyên âm thứ 2 (hòa, hòe, thúy)
                if ((v1 == 'o' && (v2 == 'a' || v2 == 'e')) || (v1 == 'u' && v2 == 'y'))
                {
                    return vowelPositions[1];
                }

                // ia, ya, ua, ưa: dấu ở nguyên âm thứ nhất (mía, của, chứa, tỷ)
                return vowelPositions[0];
            }

            // Nếu có 3 nguyên âm (oai, oay, uya, ươu, ...): đặt ở nguyên âm giữa
            if (count >= 3)
            {
                return vowelPositions[1];
            }

            return vowelPositions[0];
        }

        private static bool IsVowel(char c)
        {
            for (int r = 0; r < ToneTable.Length; r++)
            {
                for (int col = 0; col < ToneTable[r].Length; col++)
                {
                    if (ToneTable[r][col] == c) return true;
                }
            }
            return false;
        }

        private static int GetCurrentTone(char c)
        {
            for (int r = 0; r < ToneTable.Length; r++)
            {
                for (int col = 1; col < ToneTable[r].Length; col++)
                {
                    if (ToneTable[r][col] == c) return col;
                }
            }
            return 0;
        }

        private static char RemoveTone(char c)
        {
            for (int r = 0; r < ToneTable.Length; r++)
            {
                for (int col = 1; col < ToneTable[r].Length; col++)
                {
                    if (ToneTable[r][col] == c) return ToneTable[r][0];
                }
            }
            return c;
        }

        private static char ApplyToneToChar(char c, int toneIndex)
        {
            char baseChar = RemoveTone(c);
            for (int r = 0; r < ToneTable.Length; r++)
            {
                if (ToneTable[r][0] == baseChar)
                {
                    if (toneIndex >= 0 && toneIndex < ToneTable[r].Length)
                    {
                        return ToneTable[r][toneIndex];
                    }
                }
            }
            return c;
        }
    }
}
