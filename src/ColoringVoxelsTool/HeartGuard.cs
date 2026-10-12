using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 「删除存档」按钮的二次确认（3D 版）。
    ///
    /// 游戏里点删除存档会直接删掉当前关卡存档并回主菜单，不可撤销。这里在它前面加一道确认：
    /// 点删除时先不执行，等用户在插件弹窗里再确认一次（确定键带 2 秒冷静期）。
    /// 目标类型 / 方法名在找不到时只记一条日志，不影响其它功能。
    /// </summary>
    internal static class HeartGuard
    {
        /// <summary>是否正在等待用户二次确认（面板每帧读取来决定画不画弹窗）。</summary>
        public static bool Pending { get; private set; }

        /// <summary>确定键的冷静期结束时间（Time.unscaledTime）。</summary>
        public static float ArmAt { get; private set; }

        /// <summary>累计拦截次数。</summary>
        public static int Blocked { get; private set; }

        private static object _target;
        private static MethodInfo _original;
        private static bool _allowOnce;

        internal static void Patch(Harmony harmony)
        {
            Type type = FindType("UIButtonDeleteSave");
            if (type == null)
            {
                Log.Warn("删除存档二次确认：没有找到 UIButtonDeleteSave，已跳过");
                return;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            MethodInfo action = type.GetMethod("Action", flags, null, Type.EmptyTypes, null);
            if (action == null)
            {
                Log.Warn("删除存档二次确认：没有找到 UIButtonDeleteSave.Action，已跳过");
                return;
            }

            MethodInfo prefix = typeof(HeartGuard).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(action, new HarmonyMethod(prefix));
            Log.Info("删除存档二次确认已装配");
        }

        private static bool Prefix(object __instance, MethodInfo __originalMethod)
        {
            if (_allowOnce)
            {
                _allowOnce = false;
                return true;
            }
            if (__originalMethod == null)
            {
                // 拿不到原函数就没法在确认后补执行，宁可放行也不要卡住游戏。
                return true;
            }

            Blocked++;
            _target = __instance;
            _original = __originalMethod;
            ArmAt = Time.unscaledTime + 2f;
            Pending = true;
            return false;
        }

        /// <summary>用户在插件弹窗里点了确定：执行原本的删除逻辑。</summary>
        public static void Confirm()
        {
            object target = _target;
            MethodInfo original = _original;
            Clear();

            if (target == null || original == null) return;
            try
            {
                _allowOnce = true;
                original.Invoke(target, null);
            }
            catch (Exception e)
            {
                Log.Error("执行删除存档失败：" + e);
            }
            finally
            {
                _allowOnce = false;
            }
        }

        /// <summary>用户点了取消。</summary>
        public static void Cancel()
        {
            Clear();
        }

        private static void Clear()
        {
            Pending = false;
            _target = null;
            _original = null;
        }

        private static Type FindType(string name)
        {
            Type t = null;
            try { t = AccessTools.TypeByName(name); } catch { }
            if (t != null) return t;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type candidate in asm.GetTypes())
                        if (candidate.Name == name) return candidate;
                }
                catch { }
            }
            return null;
        }
    }
}
