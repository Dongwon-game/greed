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
            Debug.Log("Greed bundled Korean fonts passed: both weights, " + characters.Count
                + " source characters including all 11,172 Hangul syllables, four sample sizes, included font data and original license.");
        }

        [MenuItem("Greed Last/Check Bundled Korean Fonts", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

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
