using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeFontCheck
    {
        private const string FontDirectory = "Assets/Resources/GreedLast/Fonts/";

        [MenuItem("Greed Last/Check Bundled Korean Fonts")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Font checks require Edit mode.");
            VerifyOriginalAssets();
            var characters = new SortedSet<char>();
            for (char c = (char)0xAC00; c <= (char)0xD7A3; c++) characters.Add(c);
            foreach (string file in Directory.GetFiles("Assets/Scripts/GreedLast", "*.cs", SearchOption.AllDirectories))
                foreach (char c in File.ReadAllText(file))
                    if (!char.IsControl(c) && !char.IsWhiteSpace(c)) characters.Add(c);
            string sample = "피라미드 입구 저장 조합 무한모드 정확 체력 집중 점수 좌 중 우 "
                + "GREED LAST S1 S2 S3 0123456789 +-% ± × · → ← ↑ ↓ ↔ ⭯ ⭮ ★";
            Font symbols = Resources.Load<Font>("GreedLast/Fonts/NotoSansSymbols2-Regular");
            var symbolImporter = AssetImporter.GetAtPath(FontDirectory + "NotoSansSymbols2-Regular.ttf") as TrueTypeFontImporter;
            Require(symbols != null && symbols.dynamic && symbolImporter != null && symbolImporter.includeFontData
                && File.Exists("Assets/ThirdParty/NotoSansSymbols2/OFL.txt"),
                "Rotation symbols need their own included font data and original license.");
            foreach (string name in new[] { "NotoSansKR-Regular", "NotoSansKR-Bold" })
            {
                string path = FontDirectory + name + ".otf";
                var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                Font font = Resources.Load<Font>("GreedLast/Fonts/" + name);
                Require(importer != null && font != null && font.dynamic
                    && importer.fontTextureCase == FontTextureCase.Dynamic && importer.includeFontData,
                    "The Korean UI font must be a bundled Dynamic font, not an OS-installed dependency: " + name);
                Require(Array.IndexOf(importer.fontReferences, symbols) >= 0,
                    "Each Korean weight must reference the bundled symbol fallback: " + name);
                Require(Array.IndexOf(importer.fontNames, "Noto Sans Symbols 2") >= 0,
                    "UI Text needs the bundled fallback family name as well as its font reference: " + name);
                foreach (char c in characters)
                    Require(font.HasCharacter(c), name + " lacks U+" + ((int)c).ToString("X4"));
                foreach (int size in new[] { 17, 29, 48, 72 })
                {
                    font.RequestCharactersInTexture(sample, size, FontStyle.Normal);
                    symbols.RequestCharactersInTexture("⭯⭮", size, FontStyle.Normal);
                    foreach (char c in sample)
                        if (!char.IsWhiteSpace(c))
                            Require((c == '⭯' || c == '⭮' ? symbols : font).GetCharacterInfo(
                                c, out CharacterInfo glyph, size, FontStyle.Normal)
                                && glyph.advance > 0,
                                name + " cannot render U+" + ((int)c).ToString("X4") + " at " + size);
                }
            }
            Require(File.Exists("Assets/ThirdParty/NotoSansKR/OFL.txt"), "The font's original OFL license must accompany the assets.");
            var host = new GameObject("GreedBundledFontFactoryCheck");
            try
            {
                MethodInfo factory = typeof(GreedLastRuntimeUi).GetMethod("CreateText", BindingFlags.NonPublic | BindingFlags.Static);
                foreach (FontStyle style in new[] { FontStyle.Normal, FontStyle.Bold })
                {
                    var label = (Text)factory.Invoke(null, new object[] { host.transform, "FontCheck", sample,
                        29, TextAnchor.MiddleCenter, style });
                    Font expected = Resources.Load<Font>("GreedLast/Fonts/NotoSansKR-" + (style == FontStyle.Bold ? "Bold" : "Regular"));
                    Require(label.font == expected && label.fontStyle == FontStyle.Normal,
                        "Text creation must choose the real bundled weight without synthesizing bold twice.");
                    foreach (char c in "⭯⭮")
                    {
                        symbols.RequestCharactersInTexture(c.ToString(), 29, FontStyle.Normal);
                        Require(symbols.GetCharacterInfo(c, out CharacterInfo glyph, 29, FontStyle.Normal),
                            "The bundled fallback must render its circle arrow directly.");
                        label.text = c.ToString();
                        Require(Mathf.Abs(label.preferredWidth - glyph.advance) <= 1f,
                            "Text must resolve the bundled fallback arrow, not an OS substitution or missing-glyph box: "
                            + style + " / U+" + ((int)c).ToString("X4") + " / actual " + label.preferredWidth
                            + " / expected " + glyph.advance + " / pixelsPerUnit " + label.pixelsPerUnit);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            VerifyTextCompleteness();
            Debug.Log("Greed bundled Korean fonts passed: both weights, " + characters.Count
                + " source characters including all 11,172 Hangul syllables, four sample sizes, included font data and original license.");
        }

        [MenuItem("Greed Last/Check Bundled Korean Fonts", true)]
        [MenuItem("Greed Last/Run Text Completeness Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static bool IsTextComplete(Text label, out int visibleCount, out int completeCount, out int fontSize)
        {
            visibleCount = completeCount = fontSize = 0;
            if (label == null || label.font == null) return false;
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            using (var visible = new TextGenerator())
            using (var complete = new TextGenerator())
            {
                if (!visible.Populate(label.text, settings)) return false;
                fontSize = label.resizeTextForBestFit ? visible.fontSizeUsedForBestFit : label.fontSize;
                if (fontSize <= 0) return false;
                visibleCount = visible.characterCountVisible;
                // Keep the rendered best-fit size; only remove vertical truncation for the comparison.
                settings.resizeTextForBestFit = false;
                settings.fontSize = fontSize;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                if (!complete.Populate(label.text, settings)) return false;
                completeCount = complete.characterCountVisible;
                return visibleCount == completeCount;
            }
        }

        [MenuItem("Greed Last/Run Text Completeness Check")]
        public static void VerifyTextCompleteness()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Text completeness checks require Edit mode.");
            var host = new GameObject("GreedTextCompletenessCheck", typeof(RectTransform), typeof(Canvas));
            int completeCases = 0, clippedCases = 0;
            try
            {
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var label = new GameObject("TextCompletenessFixture", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(host.transform, false);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.alignment = TextAnchor.UpperLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.supportRichText = true;
                foreach (string weight in new[] { "Regular", "Bold" })
                foreach (float scale in new[] { 1f, 2f })
                {
                    canvas.scaleFactor = scale;
                    label.font = Resources.Load<Font>("GreedLast/Fonts/NotoSansKR-" + weight);
                    Require(label.font != null, "Text completeness fixtures need the included Korean font.");
                    foreach (string sample in new[] { "저장 슬롯 선택", "일반 런\n저장 조합\n무한 시작",
                        "<color=#FFD569>저장 조합</color>\n<b>선택 확인</b>", "저장 슬롯을 선택하세요" })
                    {
                        label.text = sample;
                        label.fontSize = 44;
                        label.resizeTextForBestFit = sample == "저장 슬롯을 선택하세요";
                        label.resizeTextMinSize = 17;
                        label.resizeTextMaxSize = 44;
                        label.rectTransform.sizeDelta = label.resizeTextForBestFit
                            ? new Vector2(220f, 70f) : new Vector2(420f, 240f);
                        Require(IsTextComplete(label, out int visible, out int full, out int size)
                            && visible > 0 && full == visible && size > 0,
                            "Complete Korean, multiline, rich-text and best-fit labels must not be rejected: " + weight + "/" + scale + "/" + sample);
                        completeCases++;
                    }
                    foreach (bool bestFit in new[] { false, true })
                    {
                        label.text = "SAVE\nAGAIN\nEXIT";
                        label.fontSize = 44;
                        label.resizeTextForBestFit = bestFit;
                        label.resizeTextMinSize = 40;
                        label.resizeTextMaxSize = 44;
                        label.rectTransform.sizeDelta = new Vector2(420f, 65f);
                        Require(!IsTextComplete(label, out int visible, out int full, out int size)
                            && visible > 0 && visible < full && size > 0,
                            "A vertically truncated fixed/bounded-best-fit label must be rejected: " + weight + "/" + scale);
                        label.cachedTextGenerator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size));
                        Rect rect = label.rectTransform.rect;
                        foreach (UIVertex vertex in label.cachedTextGenerator.verts)
                        {
                            Vector3 point = vertex.position / label.pixelsPerUnit;
                            Require(point.x >= rect.xMin - 3f && point.x <= rect.xMax + 3f
                                && point.y >= rect.yMin - 3f && point.y <= rect.yMax + 3f,
                                "The clipped fixture must reproduce the old in-bounds-only false positive.");
                        }
                        clippedCases++;
                    }
                }
                Require(completeCases == 16 && clippedCases == 8, "Text completeness must cover both weights and pixel scales.");
                Debug.Log("Greed text completeness passed: 16 Korean/multiline/rich-text/best-fit positive controls and 8 clipped fixed/bounded-best-fit labels whose remaining geometry still fits. Native text generators at both weights and two canvas pixel scales; no screenshot, hardware input or performance claim.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void VerifyOriginalAssets()
        {
            var originals = new Dictionary<string, string>
            {
                { FontDirectory + "NotoSansKR-Regular.otf", "69975A0AC8472717870AEFEAB0A4D52739308D90856B9955313B2AD5E0148D68" },
                { FontDirectory + "NotoSansKR-Bold.otf", "5A6CEB287ED2FC6CFC6213144EBEA68CBD94B20FC9EB873D8486493BF02D9BDA" },
                { FontDirectory + "NotoSansSymbols2-Regular.ttf", "7D5FB73B7CA67A6798101741F5D280A3D016A56A197AFCD4199DBB57B4B82A21" },
                { "Assets/ThirdParty/NotoSansKR/OFL.txt", "6A73F9541C2DE74158C0E7CF6B0A58EF774F5A780BF191F2D7EC9CC53EFE2BF2" },
                { "Assets/ThirdParty/NotoSansSymbols2/OFL.txt", "B118DD41337806A5D4797052C77CAF3BD096AED783E5EB21B4D11154351E1AC0" },
            };
            foreach (var original in originals)
                using (var stream = File.OpenRead(original.Key))
                using (var sha = SHA256.Create())
                    Require(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty) == original.Value,
                        "The pinned original font/license changed; review provenance and glyph coverage before updating: " + original.Key);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
