using System;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 「人工辅助」的一键识别（3D 版）：把当前层的画布区域与格子大小算出来。
    ///
    /// 直接读游戏自己的体素数据 —— 当前层每个体素在世界坐标里就是一个单位方块，
    /// 用主相机把三个相邻体素的中心投影到屏幕，即可推出「格子 → 屏幕」的仿射变换，
    /// 从而算出整层的四角、每格多少像素、以及行列数。
    ///
    /// 注意：3D 版的人工辅助扫描引擎尚未迁移，这里只提供区域识别；
    /// 透视相机下若画布有旋转，识别精度有限。
    /// </summary>
    internal static class AssistAutoDetect
    {
        /// <summary>识别结果。四角顺序：左上 / 右上 / 右下 / 左下（桌面坐标）。</summary>
        internal struct Result
        {
            public bool Ok;
            public string Message;
            public double[] X;
            public double[] Y;
            public double CellWidth;
            public double CellHeight;
            public int Cols;
            public int Rows;
        }

        private const double OutsideTolerance = 4.0;

        /// <param name="originX">游戏客户区左上角在桌面上的偏移（全屏 / 无边框时为 0）。</param>
        /// <param name="originY">同上。</param>
        public static Result Detect(float originX, float originY)
        {
            var res = new Result { X = new double[4], Y = new double[4] };

            var b = GameApi.B;
            if (!GameApi.InLevel(b))
            {
                res.Message = "还没进入关卡：请先打开一张图，让画布显示出来再点识别";
                return res;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                res.Message = "读不到游戏相机，无法换算屏幕坐标";
                return res;
            }

            if (!GameApi.Size(b, out int cols, out int _, out int rows) || cols <= 0 || rows <= 0)
            {
                res.Message = "读不到画布尺寸，请先进入关卡再识别";
                return res;
            }

            int layer = b.selectedLayer >= 0 ? b.selectedLayer : 0;

            Vector2 c00, c10, c01;
            if (!CellCenter(cam, 0, layer, 0, out c00) ||
                !CellCenter(cam, 1, layer, 0, out c10) ||
                !CellCenter(cam, 0, layer, 1, out c01))
            {
                res.Message = "画布不在视野内：请先缩放 / 转动视角，让整张画布显示出来";
                return res;
            }

            Vector2 eu = c10 - c00;     // 每 +1 格（x 方向）的屏幕位移
            Vector2 ev = c01 - c00;     // 每 +1 格（z 方向）的屏幕位移

            double cellW = eu.magnitude;
            double cellH = ev.magnitude;
            if (cellW < 2.0 || cellH < 2.0)
            {
                res.Message = "格子太小了（不到 2 像素），请把画布放大一点再识别";
                return res;
            }

            // 以格为单位：当前层左上角是 (-0.5, rows-0.5)，右下角是 (cols-0.5, -0.5)。
            double[] tileX = { -0.5, cols - 0.5, cols - 0.5, -0.5 };
            double[] tileY = { rows - 0.5, rows - 0.5, -0.5, -0.5 };

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            for (int i = 0; i < 4; i++)
            {
                double gx = c00.x + eu.x * tileX[i] + ev.x * tileY[i];
                double gy = c00.y + eu.y * tileX[i] + ev.y * tileY[i];

                if (gx < minX) minX = gx;
                if (gx > maxX) maxX = gx;
                if (gy < minY) minY = gy;
                if (gy > maxY) maxY = gy;

                res.X[i] = gx + originX;
                res.Y[i] = gy + originY;
            }

            if (minX < -OutsideTolerance || minY < -OutsideTolerance ||
                maxX > Screen.width + OutsideTolerance || maxY > Screen.height + OutsideTolerance)
            {
                res.Message = string.Format(
                    "画布没有完整显示（{0:0} × {1:0} px，超出了屏幕），请先缩放或调整视角，让整张画布可见",
                    maxX - minX, maxY - minY);
                return res;
            }

            res.Ok = true;
            res.CellWidth = cellW;
            res.CellHeight = cellH;
            res.Cols = cols;
            res.Rows = rows;
            res.Message = string.Format("{0} × {1} 格 · 格子 {2:0.0} × {3:0.0} px",
                cols, rows, cellW, cellH);
            return res;
        }

        /// <summary>体素 (x, y, z) 中心对应的屏幕点（GUI 坐标，左上为原点）。</summary>
        private static bool CellCenter(Camera cam, int x, int y, int z, out Vector2 gui)
        {
            gui = Vector2.zero;
            try
            {
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(x, y, z));
                if (sp.z <= 0f) return false;
                gui = new Vector2(sp.x, Screen.height - sp.y);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
