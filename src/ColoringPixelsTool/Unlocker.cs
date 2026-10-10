using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColoringPixelsTool
{
    /// <summary>
    /// 《Coloring Voxels》：世界（书籍）/ 成就 / DLC 的解锁逻辑。
    ///
    /// 世界可见性 = WorldData.visibility；关卡完成 = mainMenuSaveData.worldDatas[world] 里的 HighLevelLevelData.completed；
    /// DLC = SteamManager.CheckIfProductOwned（由 Harmony 前缀读 ForceDlcOwned 强行放行）。
    /// </summary>
    internal static class Unlocker
    {
        /// <summary>由 Harmony 前缀读取，为 true 时 DLC 拥有判定恒为真。</summary>
        public static bool ForceDlcOwned;

        // ------------------------------------------------------------- 世界

        /// <summary>让所有世界（含隐藏 / DLC）在菜单中可见。</summary>
        public static int RevealAllWorlds()
        {
            WorldData[] worlds = GameApi.St?.worldDatas;
            if (worlds == null) return 0;

            int n = 0;
            foreach (WorldData wd in worlds)
            {
                if (wd == null) continue;
                if (wd.visibility != WorldData.WorldVisibility.Always)
                {
                    wd.visibility = WorldData.WorldVisibility.Always;
                    n++;
                }
            }
            RefreshMenus();
            return n;
        }

        /// <summary>把所有世界里的所有关卡标记为「已完成」。</summary>
        public static int CompleteAllWorlds()
        {
            CrossLevelStorage st = GameApi.St;
            MainMenuSaveData mm = st?.mainMenuSaveData;
            WorldData[] worlds = st?.worldDatas;
            if (st == null || mm == null || mm.worldDatas == null || worlds == null) return 0;

            int n = 0;
            int count = Math.Min(mm.worldDatas.Length, worlds.Length);
            for (int i = 0; i < count; i++)
            {
                WorldData wd = worlds[i];
                List<LevelData> levels = wd != null ? wd.levels : null;
                if (levels == null) continue;

                for (int j = 0; j < levels.Count; j++)
                {
                    HighLevelLevelData hl = mm.worldDatas[i].GetHighLevelLevelData(levels[j]);
                    if (hl.completed) continue;
                    hl.completed = true;
                    if (hl.personalBest <= 0f) hl.personalBest = 1f;
                    hl.completionPercentage = 1f;
                    mm.worldDatas[i].SetHighLevelLevelData(hl);
                    n++;
                }
            }

            try { SaveManager.SaveMainMenuData(); }
            catch (Exception e) { Debug.LogWarning("[CGV] 保存菜单失败：" + e.Message); }
            RefreshMenus();
            return n;
        }

        // ------------------------------------------------------------- 成就

        /// <summary>解锁全部 Steam 成就（世界成就 + 每一关的成就）。</summary>
        public static int UnlockAllAchievements()
        {
            CrossLevelStorage st = GameApi.St;
            SteamManager steam = SafeSteam();
            WorldData[] worlds = st?.worldDatas;
            if (st == null || steam == null || worlds == null) return 0;

            HashSet<string> ids = new HashSet<string>();
            foreach (WorldData wd in worlds)
            {
                if (wd == null) continue;
                if (!string.IsNullOrEmpty(wd.achievementName)) ids.Add(wd.achievementName);

                List<LevelData> levels = wd.levels;
                if (levels == null) continue;
                foreach (LevelData lv in levels)
                    if (!string.IsNullOrEmpty(lv.achievementName)) ids.Add(lv.achievementName);
            }

            int n = 0;
            foreach (string id in ids)
            {
                try
                {
                    steam.UnlockSteamAchievement(id);
                    n++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[CGV] 成就解锁失败 " + id + "：" + e.Message);
                }
            }
            return n;
        }

        // ------------------------------------------------------------- 工具

        private static SteamManager SafeSteam()
        {
            try { return SteamManager.Inst; }
            catch (Exception) { return null; }
        }

        private static void RefreshMenus()
        {
            try { WorldSelectPopulator.Inst?.InitialiseButtons(); }
            catch (Exception e) { Debug.LogWarning("[CGV] 刷新主菜单失败：" + e.Message); }
        }
    }
}
