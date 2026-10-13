using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 中文字形兜底补丁（不做任何文案翻译）。
    ///
    /// 只对界面上「含中日韩字符」的 uGUI <see cref="Text"/> 临时换成系统中文字体；
    /// 一旦文本变回不含中文就恢复游戏原字体。游戏原本的字体、英文/数字显示一律不动。
    ///
    /// 性能：<c>FindObjectsOfType&lt;Text&gt;</c> 会遍历场景全部 GameObject，在体素关卡里
    /// （每格一个方块）非常贵。所以这里改为**随场景加载的有界扫描**：
    ///   · 每次切场景后只扫有限几次；
    ///   · 若从没发现过 uGUI Text（本作主要用 TMP），扫完预算就彻底停下，不再周期扫描；
    ///   · 一旦发现过 Text（说明游戏确实用 uGUI Text），才恢复 0.5s 周期扫描。
    /// </summary>
    internal class GameFontFallback : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;
        private const int ScansPerScene = 6;

        private sealed class Saved
        {
            public Text Target;
            public Font OriginalFont;
            public Material OriginalMaterial;
        }

        private readonly List<Saved> _patched = new List<Saved>();
        private readonly HashSet<int> _seen = new HashSet<int>();

        private float _next;
        private int _budget = ScansPerScene;
        private bool _usesText;   // 曾经扫到过 uGUI Text

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 新场景的 UI 换了，重新给几次扫描机会
            _budget = ScansPerScene;
            _next = 0f;
        }

        private void Update()
        {
            // 没用到 uGUI Text 且预算用完后：彻底不扫，避免在体素关卡里每 0.5s 全场景遍历
            if (!_usesText && _budget <= 0) return;

            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + ScanInterval;
            if (_budget > 0) _budget--;

            Font f = CjkFont.Get();
            if (f == null) return;

            Text[] texts = FindObjectsOfType<Text>();
            if (texts.Length > 0) _usesText = true;

            for (int i = 0; i < texts.Length; i++)
            {
                Text t = texts[i];
                if (t == null) continue;
                if (t.hideFlags != HideFlags.None) continue;
                if (!CjkFont.HasCjk(t.text)) continue;

                if (t.font != f)
                {
                    Remember(t);
                    t.font = f;
                    t.material = null;
                    t.SetAllDirty();
                }
            }

            RestoreNonCjk(f);
        }

        /// <summary>文本已经不含中文的，恢复游戏原本的字体与材质。</summary>
        private void RestoreNonCjk(Font cjk)
        {
            for (int i = _patched.Count - 1; i >= 0; i--)
            {
                Saved s = _patched[i];
                if (s.Target == null)
                {
                    _patched.RemoveAt(i);
                    continue;
                }

                if (CjkFont.HasCjk(s.Target.text)) continue;

                if (s.Target.font == cjk)
                {
                    s.Target.font = s.OriginalFont;
                    s.Target.material = s.OriginalMaterial;
                    s.Target.SetAllDirty();
                }

                _seen.Remove(s.Target.GetInstanceID());
                _patched.RemoveAt(i);
            }

            // 还在管着一些 Text：说明游戏确实用 uGUI Text，继续周期检查
            if (_patched.Count > 0) _usesText = true;
        }

        private void Remember(Text t)
        {
            if (!_seen.Add(t.GetInstanceID())) return;
            _patched.Add(new Saved
            {
                Target = t,
                OriginalFont = t.font,
                OriginalMaterial = t.material
            });
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _patched.Count; i++)
            {
                Saved s = _patched[i];
                if (s.Target == null) continue;
                s.Target.font = s.OriginalFont;
                s.Target.material = s.OriginalMaterial;
            }
            _patched.Clear();
            _seen.Clear();
        }
    }
}
