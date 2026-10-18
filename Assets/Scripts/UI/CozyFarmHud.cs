using UnityEngine;
using CozyFarm.Core;
using CozyFarm.Player;
using CozyFarm.Testing;

namespace CozyFarm.UI
{
    public class CozyFarmHud : MonoBehaviour
    {
        private Texture2D woodFrameTex;
        private Texture2D parchmentTex;
        private Texture2D slotActiveTex;
        private Texture2D coinGoldTex;
        private Texture2D minimapBgTex;
        private GUIStyle plaqueTextStyle;
        private GUIStyle slotBadgeStyle;
        private GUIStyle slotLabelStyle;

        private void EnsureStyles()
        {
            if (woodFrameTex != null) return;

            woodFrameTex = MakeSolidTex(new Color(0.45f, 0.27f, 0.14f, 0.98f));
            parchmentTex = MakeSolidTex(new Color(0.95f, 0.89f, 0.76f, 0.98f));
            slotActiveTex = MakeSolidTex(new Color(0.98f, 0.84f, 0.42f, 0.98f));
            coinGoldTex = MakeSolidTex(new Color(0.98f, 0.78f, 0.18f, 1.0f));
            minimapBgTex = MakeSolidTex(new Color(0.56f, 0.74f, 0.44f, 0.96f));

            plaqueTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.28f, 0.16f, 0.08f) }
            };

            slotBadgeStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeSolidTex(new Color(0.22f, 0.14f, 0.08f, 0.95f)) }
            };

            slotLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.28f, 0.16f, 0.08f) }
            };
        }

        private static Texture2D MakeSolidTex(Color c)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { c, c, c, c });
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            EnsureStyles();
            CozyFarmState state = CozyFarmState.Instance;
            if (state == null) return;

            // 1. Top-Left Coin Plaque (matching frame_01 & frame_08 coin burst)
            Rect coinOuter = new Rect(18f, 16f, 210f, 52f);
            DrawWoodPlaque(coinOuter);

            float coinBurstScale = state.coinPulseTimer > 0f
                ? 1.0f + Mathf.Sin(state.coinPulseTimer * 12f) * 0.25f
                : 1.0f;
            float coinSize = 32f * coinBurstScale;
            Rect coinIconRect = new Rect(28f - (coinSize - 32f) * 0.5f, 26f - (coinSize - 32f) * 0.5f, coinSize, coinSize);
            GUI.DrawTexture(coinIconRect, coinGoldTex);
            GUI.Label(coinIconRect, "¥", new GUIStyle(plaqueTextStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(19 * coinBurstScale),
                normal = { textColor = new Color(0.45f, 0.25f, 0.04f) }
            });

            GUI.Label(new Rect(70f, 20f, 145f, 44f), $"{state.coins:N0}", plaqueTextStyle);

            // 2. Top-Right Calendar & Season Plaque ("Day 12 · Spring ☀️")
            Rect calOuter = new Rect(Screen.width - 268f, 16f, 250f, 52f);
            DrawWoodPlaque(calOuter);
            string sunIcon = state.timeOfDay < 0.65f ? "☀" : "☾";
            GUI.Label(
                new Rect(calOuter.x + 16f, calOuter.y + 4f, calOuter.width - 24f, 44f),
                $"Day {state.day} · {state.currentSeason}  {sunIcon}",
                plaqueTextStyle
            );

            // 3. Bottom-Left / Center 6-Slot Wooden Hotbar
            float barWidth = 436f;
            float barHeight = 76f;
            Rect hotbarOuter = new Rect(22f, Screen.height - barHeight - 18f, barWidth, barHeight);
            DrawWoodPlaque(hotbarOuter);

            string[] slotTitles = { "Water", "Hoe", "Pmpk", "Pmpk★", "TomSd", "Tom★" };
            int[] slotCounts =
            {
                state.wateringCanCount,
                state.hoeCount,
                state.harvestedPumpkinCount,
                state.pumpkinSeedCount,
                state.tomatoSeedCount,
                state.harvestedTomatoCount
            };

            for (int i = 0; i < 6; i++)
            {
                float sx = hotbarOuter.x + 12f + i * 69f;
                float sy = hotbarOuter.y + 10f;
                Rect slotRect = new Rect(sx, sy, 60f, 56f);
                bool active = state.selectedHotbarSlot == i;
                GUI.DrawTexture(slotRect, active ? slotActiveTex : parchmentTex);

                if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none))
                {
                    if (FarmerController.Instance != null)
                        FarmerController.Instance.SetSelectedSlot(i);
                }

                GUI.Label(new Rect(sx + 2f, sy + 12f, 56f, 28f), slotTitles[i], slotLabelStyle);

                if (slotCounts[i] > 0)
                {
                    Rect badgeRect = new Rect(sx + 38f, sy + 36f, 20f, 18f);
                    GUI.Box(badgeRect, slotCounts[i].ToString(), slotBadgeStyle);
                }
            }

            // 4. Bottom-Right Circular / Framed Minimap + Autopilot Toggle Button
            float mapSize = 156f;
            Rect mapOuter = new Rect(Screen.width - mapSize - 20f, Screen.height - mapSize - 18f, mapSize, mapSize);
            GUI.DrawTexture(mapOuter, woodFrameTex);
            Rect mapInner = new Rect(mapOuter.x + 6f, mapOuter.y + 6f, mapSize - 12f, mapSize - 12f);
            GUI.DrawTexture(mapInner, minimapBgTex);

            // Minimap landmark markers (Barn, Windmill, Market, Bridge, Farmer)
            DrawMinimapMarker(mapInner, new Vector2(-4.5f, 5.2f), "B", new Color(0.85f, 0.22f, 0.20f));
            DrawMinimapMarker(mapInner, new Vector2(2.6f, 5.2f), "W", new Color(0.92f, 0.86f, 0.72f));
            DrawMinimapMarker(mapInner, new Vector2(6.2f, 2.2f), "M", new Color(0.95f, 0.55f, 0.18f));
            if (FarmerController.Instance != null)
            {
                Vector3 fp = FarmerController.Instance.transform.position;
                DrawMinimapMarker(mapInner, new Vector2(fp.x, fp.z), "●", new Color(0.18f, 0.45f, 0.95f));
            }

            // Small companion/autopilot button beside minimap (matching bottom-right button in @Mnilax UI)
            Rect autoBtnRect = new Rect(mapOuter.x - 46f, Screen.height - 54f, 38f, 36f);
            DrawWoodPlaque(autoBtnRect);
            bool autopilotOn = FarmAutopilotPlaytest.Instance != null && FarmAutopilotPlaytest.Instance.autopilotActive;
            if (GUI.Button(autoBtnRect, autopilotOn ? "AI" : "P", slotLabelStyle))
            {
                if (FarmAutopilotPlaytest.Instance != null)
                    FarmAutopilotPlaytest.Instance.autopilotActive = !FarmAutopilotPlaytest.Instance.autopilotActive;
            }
        }

        private void DrawWoodPlaque(Rect outer)
        {
            GUI.DrawTexture(outer, woodFrameTex);
            Rect inner = new Rect(outer.x + 5f, outer.y + 5f, outer.width - 10f, outer.height - 10f);
            GUI.DrawTexture(inner, parchmentTex);
        }

        private void DrawMinimapMarker(Rect mapRect, Vector2 worldXZ, string label, Color color)
        {
            float nx = Mathf.InverseLerp(-10f, 10f, worldXZ.x);
            float ny = 1f - Mathf.InverseLerp(-9f, 9f, worldXZ.y);
            float px = mapRect.x + nx * (mapRect.width - 20f) + 2f;
            float py = mapRect.y + ny * (mapRect.height - 20f) + 2f;
            Rect r = new Rect(px, py, 18f, 18f);
            GUI.DrawTexture(r, MakeSolidTex(color));
            GUI.Label(r, label, new GUIStyle(slotLabelStyle) { fontSize = 10, normal = { textColor = Color.white } });
        }
    }
}
