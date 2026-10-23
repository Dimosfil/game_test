using System;
using System.Collections.Generic;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Presentation
{
    // Row-major order is the authoring contract of Resources/UI/command-icons.png.
    public enum HudIcon
    {
        Worker, Marine, Depot, Barracks, Refinery, EngineeringBay,
        Armory, Command, Move, Attack, Stop, Hold,
        Patrol, Repair, Build, Rally, Stim, Cancel,
        Weapons, Armor, Technology, Minerals, Gas, Supply
    }

    public static class HudIconAtlas
    {
        public const int Columns = 6;
        public const int Rows = 4;
        public const int IconCount = Columns * Rows;
        public const string ResourcePath = "UI/command-icons";

        static readonly Dictionary<string, GUIContent> tooltips = new Dictionary<string, GUIContent>(StringComparer.Ordinal);
        static readonly Dictionary<int, GUIStyle> captions = new Dictionary<int, GUIStyle>();
        static Texture2D texture;
        static bool attempted;
        static GUIStyle tooltipStyle;
        public static bool IsReady => texture != null;

        // Called once by HUD bootstrap, never by a draw, Layout or Repaint loop.
        public static void Initialize()
        {
            if (attempted) return;
            attempted = true;
            texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null) return;
            if (texture.width % Columns != 0 || texture.height % Rows != 0 || texture.width / Columns != texture.height / Rows)
            {
                Debug.LogWarning("RTS icon atlas requires a 6 by 4 grid of square cells; labels remain available.");
                texture = null;
                return;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
        }

        public static Rect UvFor(HudIcon icon)
        {
            int tile = (int)icon;
            if (tile < 0 || tile >= IconCount) throw new ArgumentOutOfRangeException(nameof(icon));
            return new Rect((tile % Columns) / (float)Columns, 1f - (tile / Columns + 1) / (float)Rows,
                1f / Columns, 1f / Rows);
        }

        public static HudIcon ForDefinition(string id)
        {
            switch (id)
            {
                case "worker": return HudIcon.Worker;
                case "marine": return HudIcon.Marine;
                case "depot": return HudIcon.Depot;
                case "barracks": return HudIcon.Barracks;
                case "refinery": return HudIcon.Refinery;
                case "engineeringbay": return HudIcon.EngineeringBay;
                case "armory": return HudIcon.Armory;
                case "command": return HudIcon.Command;
                case "mineral": return HudIcon.Minerals;
                case "geyser": return HudIcon.Gas;
                default: return HudIcon.Build;
            }
        }

        public static HudIcon ForUpgrade(UpgradeDefinition definition) =>
            definition.DamageBonus > 0 ? HudIcon.Weapons : definition.ArmorBonus > 0 ? HudIcon.Armor : HudIcon.Technology;

        public static void Draw(Rect rect, HudIcon icon)
        {
            if (texture == null || Event.current.type != EventType.Repaint) return;
            var uv = UvFor(icon);
            // Half-texel inset prevents neighboring cell bleed without slicing pixels.
            uv.xMin += .5f / texture.width; uv.xMax -= .5f / texture.width;
            uv.yMin += .5f / texture.height; uv.yMax -= .5f / texture.height;
            var previous = GUI.color;
            if (!GUI.enabled) GUI.color = previous * new Color(.43f, .48f, .52f, .85f);
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            GUI.color = previous;
        }

        public static bool Button(Rect rect, HudIcon icon, string caption, float scale, string tooltip = null)
        {
            bool clicked = GUI.Button(rect, Tooltip(tooltip ?? caption), GUIStyle.none);
            bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Fill(rect, hovered ? new Color(.10f, .23f, .28f, .99f) : new Color(.045f, .095f, .13f, .99f));
            Border(rect, hovered ? new Color(.35f, .85f, .94f) : new Color(.18f, .36f, .42f));
            float inset = Mathf.Max(3, 4 * scale);
            float side = Mathf.Max(0, Mathf.Min(rect.height - inset * 2, rect.width * .31f));
            var image = new Rect(rect.x + inset, rect.y + (rect.height - side) * .5f, side, side);
            Draw(image, icon);
            Fill(new Rect(rect.x + 1, rect.y + 1, 2 * scale, rect.height - 2),
                GUI.enabled ? new Color(.28f, .70f, .79f, .9f) : new Color(.19f, .27f, .30f));
            var label = new Rect(image.xMax + 6 * scale, rect.y + inset, rect.xMax - image.xMax - inset - 6 * scale, rect.height - inset * 2);
            int pixels = Mathf.RoundToInt((rect.height < 43 * scale ? 10 : 12) * scale);
            GUI.Label(label, caption, Caption(pixels));
            return clicked;
        }

        public static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }

        public static void Border(Rect rect, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 1), color);
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            Fill(new Rect(rect.x, rect.y, 1, rect.height), color);
            Fill(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        public static void DrawTooltip(float scale)
        {
            if (string.IsNullOrEmpty(GUI.tooltip) || Event.current.type != EventType.Repaint) return;
            if (tooltipStyle == null) tooltipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, wordWrap = true, padding = new RectOffset(10, 10, 7, 7),
                normal = { textColor = new Color(.90f, .96f, .98f) }
            };
            var mouse = Event.current.mousePosition;
            float width = Mathf.Min(310 * scale, Screen.width - 24);
            var rect = new Rect(Mathf.Clamp(mouse.x + 14 * scale, 12, Screen.width - width - 12),
                Mathf.Max(66, mouse.y - 56 * scale), width, 48 * scale);
            Fill(rect, new Color(.025f, .055f, .075f, .99f));
            Border(rect, new Color(.25f, .55f, .63f));
            GUI.Label(rect, GUI.tooltip, tooltipStyle);
        }

        static GUIContent Tooltip(string text)
        {
            if (!tooltips.TryGetValue(text, out var content))
            {
                content = new GUIContent(string.Empty, text);
                tooltips.Add(text, content);
            }
            return content;
        }

        static GUIStyle Caption(int size)
        {
            if (!captions.TryGetValue(size, out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                    wordWrap = true, richText = false, padding = new RectOffset(0, 0, 0, 0)
                };
                style.normal.textColor = new Color(.86f, .94f, .96f);
                captions.Add(size, style);
            }
            return style;
        }
    }
}
