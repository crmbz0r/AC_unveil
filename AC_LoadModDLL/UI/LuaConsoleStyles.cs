using System.Collections.Generic;
using UnityEngine;

namespace AiComi_LuaMod;

public partial class LuaConsole
{
    // Styles – jeden Frame neu zuweisen (billig); Texturen permanent cachen (teuer)
    private GUIStyle? _styleOutput,
        _styleInput,
        _styleBtn,
        _styleBtnActive,
        _styleWindow,
        _styleToggle;

    // Texturen permanent cachen – überleben GC und Skin-Reset
    private readonly Dictionary<int, Texture2D> _texCache = new();

    // Custom fonts
    private Font? _monoFont;

    // ── Marquee / LED Laufschrift ─────────────────────────────
    private GUIStyle? _styleRainbow;
    private float _marqueeOffset = 0f;
    private const float MARQUEE_SPEED = 55f;

    private void InitFonts()
    {
        if (_monoFont != null)
            return;
        var configFont = ConsoleConfig.Initialized ? ConsoleConfig.FontName.Value : "Cascadia Mono";
        var configSize = ConsoleConfig.Initialized ? ConsoleConfig.FontSize.Value : 16;
        _monoFont = Font.CreateDynamicFontFromOSFont(
            new[] { configFont, "Cascadia Mono", "Consolas", "Courier New" },
            configSize
        );
    }

    private Texture2D Tex(Color col)
    {
        int key = col.GetHashCode();
        if (_texCache.TryGetValue(key, out var existing) && existing != null)
            return existing;
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(t);
        _texCache[key] = t;
        return t;
    }

    private void DrawRainbowTitle(Rect area, string text)
    {
        if (_styleRainbow == null)
        {
            _styleRainbow = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
        }

        bool rainbow = !ConsoleConfig.Initialized || ConsoleConfig.RainbowEnabled.Value;
        bool marquee = !ConsoleConfig.Initialized || ConsoleConfig.MarqueeEnabled.Value;

        // Static (non-rainbow) mode: draw entire string in a single color
        if (!rainbow)
        {
            var titleCol = ConsoleConfig.Initialized
                ? ConsoleConfig.GetAccentColor()
                : new Color(0.4f, 1f, 0.4f);
            _styleRainbow.normal.textColor = titleCol;

            if (!marquee)
            {
                GUI.Label(new Rect(area.x + 4f, area.y + 2f, area.width - 8f, area.height - 2f),
                    text, _styleRainbow);
            }
            else
            {
                float charW = _styleRainbow.CalcSize(new GUIContent("W")).x;
                float totalW = charW * text.Length;
                _marqueeOffset += MARQUEE_SPEED * Time.deltaTime;
                if (_marqueeOffset > totalW) _marqueeOffset = 0f;
                float startX = area.width - _marqueeOffset;

                GUI.BeginGroup(new Rect(area.x, area.y, area.width, area.height));
                for (int pass = 0; pass < 2; pass++)
                {
                    float offX = pass == 0 ? 0f : -totalW;
                    float xPos = startX + offX;
                    if (xPos < area.width && xPos + totalW > 0)
                        GUI.Label(new Rect(xPos, 2f, totalW + 4f, area.height - 2f),
                            text, _styleRainbow);
                }
                GUI.EndGroup();
            }
            _styleRainbow.normal.textColor = Color.white;
            return;
        }

        // Rainbow mode
        float charWidth = _styleRainbow.CalcSize(new GUIContent("W")).x;
        float totalWidth = charWidth * text.Length;

        if (marquee)
        {
            _marqueeOffset += MARQUEE_SPEED * Time.deltaTime;
            if (_marqueeOffset > totalWidth) _marqueeOffset = 0f;
        }
        else
        {
            _marqueeOffset = 0f;
        }

        float speed = 0.4f;
        float wave = 0.03f;
        float startXR = marquee ? area.width - _marqueeOffset : 4f;

        GUI.BeginGroup(new Rect(area.x, area.y, area.width, area.height));

        int passes = marquee ? 2 : 1;
        for (int pass = 0; pass < passes; pass++)
        {
            float offsetX = pass == 0 ? 0f : -totalWidth;

            for (int i = 0; i < text.Length; i++)
            {
                float xPos = startXR + i * charWidth + offsetX;
                if (xPos > -charWidth && xPos < area.width)
                {
                    float hue = ((i * wave - Time.realtimeSinceStartup * speed) % 1f);
                    if (hue < 0)
                        hue += 1f;
                    _styleRainbow.normal.textColor = Color.HSVToRGB(hue, 1f, 1f);
                    GUI.Label(
                        new Rect(xPos, 2f, charWidth + 2f, area.height - 2f),
                        text[i].ToString(),
                        _styleRainbow
                    );
                }
            }
        }
        GUI.EndGroup();
        _styleRainbow.normal.textColor = Color.white;
    }

    private void InitStyles()
    {
        InitFonts();
        var bgInput = new Color(0.10f, 0.10f, 0.11f, 1f);
        var bgBtn = new Color(0.22f, 0.22f, 0.25f, 1f);
        var bgBtnHov = new Color(0.30f, 0.30f, 0.35f, 1f);
        var textMain = new Color(0.92f, 0.92f, 0.92f);
        var accent = new Color(0.25f, 0.25f, 0.30f, 1f);
        var accentCol = ConsoleConfig.Initialized ? ConsoleConfig.GetAccentColor() : new Color(0.4f, 1f, 0.4f);

        _styleWindow = new GUIStyle(GUI.skin.window) { fontSize = 17, fontStyle = FontStyle.Bold };
        _styleWindow.normal.background = null;
        _styleWindow.onNormal.background = null;
        _styleWindow.normal.textColor = new Color(0, 0, 0, 0);

        var cfgFontSize = ConsoleConfig.Initialized ? ConsoleConfig.FontSize.Value : 16;
        var cfgTextCol = ConsoleConfig.Initialized ? ConsoleConfig.GetAccentColor() : textMain;
        var cfgWordWrap = !ConsoleConfig.Initialized || ConsoleConfig.OutputWordWrap.Value;

        _styleOutput = new GUIStyle(GUI.skin.textArea)
        {
            font = _monoFont,
            fontSize = cfgFontSize,
            wordWrap = cfgWordWrap,
            richText = false,
        };
        _styleOutput.normal.background = Tex(bgInput);
        _styleOutput.focused.background = Tex(bgInput);
        _styleOutput.normal.textColor = cfgTextCol;
        _styleOutput.focused.textColor = cfgTextCol;

        _styleInput = new GUIStyle(GUI.skin.textArea)
        {
            font = _monoFont,
            fontSize = cfgFontSize + 1,
            wordWrap = true,
        };
        _styleInput.normal.background = Tex(bgInput);
        _styleInput.focused.background = Tex(new Color(0.12f, 0.12f, 0.16f, 1f));
        _styleInput.normal.textColor = Color.white;
        _styleInput.focused.textColor = Color.white;

        _styleBtn = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
        };
        _styleBtn.normal.background = Tex(bgBtn);
        _styleBtn.hover.background = Tex(bgBtnHov);
        _styleBtn.active.background = Tex(accent);
        _styleBtn.normal.textColor = textMain;
        _styleBtn.hover.textColor = Color.white;

        _styleBtnActive = new GUIStyle(_styleBtn) { fontStyle = FontStyle.Bold };
        _styleBtnActive.normal.background = Tex(new Color(accentCol.r * 0.35f, accentCol.g * 0.35f, accentCol.b * 0.35f, 1f));
        _styleBtnActive.hover.background = Tex(new Color(accentCol.r * 0.45f, accentCol.g * 0.45f, accentCol.b * 0.45f, 1f));
        _styleBtnActive.normal.textColor = accentCol;
        _styleBtnActive.hover.textColor = accentCol;

        _styleToggle = new GUIStyle(GUI.skin.toggle) { fontSize = 16 };
        _styleToggle.normal.textColor = textMain;
        _styleToggle.hover.textColor = Color.white;
    }
}
