using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool frameRequested, preciseTimer;
        private FormWindowState previousWindowState;
        private volatile bool frameTimingDisposed;
        private System.Threading.Timer frameTimer;
        private int frameWakeQueued;
        private double nextFrameAt, frameCpuMs, lastFrameAt, frameIntervalMs;
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);
        private void InitializeFrameTiming()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) preciseTimer = timeBeginPeriod(1) == 0;
            frameTimer = new System.Threading.Timer(_ => WakeFrame(), null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }
        private void DisposeFrameTiming() { frameTimingDisposed = true; if (frameTimer != null) frameTimer.Dispose(); if (preciseTimer) { timeEndPeriod(1); preciseTimer = false; } }
        // Coalesce input/simulation updates; idle windows need no continuous repaint.
        public new void Invalidate() { frameRequested = true; ScheduleFrame(); }
        private void ScheduleFrame()
        {
            if (frameTimingDisposed || frameTimer == null || !IsHandleCreated || System.Threading.Volatile.Read(ref frameWakeQueued) != 0) return;
            int delay = (int)Math.Max(0, Math.Min(1000, Math.Ceiling(nextFrameAt - uiTime.Elapsed.TotalMilliseconds)));
            frameTimer.Change(delay, System.Threading.Timeout.Infinite);
        }
        private void WakeFrame()
        {
            if (frameTimingDisposed || System.Threading.Interlocked.Exchange(ref frameWakeQueued, 1) != 0) return;
            try { BeginInvoke((Action)(() => {
                System.Threading.Interlocked.Exchange(ref frameWakeQueued, 0);
                if (frameTimingDisposed) return;
                if (FrameDue(uiTime.Elapsed.TotalMilliseconds)) base.Invalidate();
                else if (frameRequested) ScheduleFrame();
            })); } catch (InvalidOperationException) { System.Threading.Interlocked.Exchange(ref frameWakeQueued, 0); }
        }
        internal bool FrameDue(double now)
        {
            if (!frameRequested || now < nextFrameAt) return false;
            frameRequested = false;
            double interval = prefs.FrameLimit == 0 ? 0 : 1000.0 / prefs.FrameLimit;
            nextFrameAt = now > nextFrameAt + interval ? now + interval : nextFrameAt + interval; return true;
        }
        private void DrawPerformance(Graphics g, double began)
        {
            double now = uiTime.Elapsed.TotalMilliseconds;
            frameCpuMs = frameCpuMs == 0 ? now - began : frameCpuMs * .85 + (now - began) * .15;
            if (lastFrameAt > 0 && now - lastFrameAt < 250) frameIntervalMs = frameIntervalMs == 0 ? now - lastFrameAt : frameIntervalMs * .85 + (now - lastFrameAt) * .15;
            lastFrameAt = now;
            if (!prefs.PerformanceOverlay) return;
            Fill(g, Color.FromArgb(230, 15, 24, 26), 12, VH - 86, 360, 43);
            Txt(g, "Кадр CPU " + frameCpuMs.ToString("0.0") + " мс  ·  обновления " + (frameIntervalMs > 0 ? (1000 / frameIntervalMs).ToString("0") : "—") + "/с", 22, VH - 80, 11, Palette.Gold);
            Txt(g, "При движении · лимит " + (prefs.FrameLimit == 0 ? "выкл." : prefs.FrameLimit.ToString()) + " · карта " + (!AtlasBusy ? "готова" : "детализация…"), 22, VH - 62, 10, Palette.Muted);
        }
        private void CycleResolution()
        {
            if (fullScreen) return;
            Size[] sizes = { new Size(1280,720), new Size(1600,900), new Size(1920,1080), new Size(2560,1440) };
            int i = Array.FindIndex(sizes, s => s.Width == prefs.WindowWidth && s.Height == prefs.WindowHeight);
            Size desired = sizes[(i + 1) % sizes.Length], area = System.Windows.Forms.Screen.FromControl(this).WorkingArea.Size;
            WindowState = FormWindowState.Normal;
            ClientSize = new Size(Math.Min(desired.Width, area.Width - 24), Math.Min(desired.Height, area.Height - 64));
            prefs.WindowWidth = ClientSize.Width; prefs.WindowHeight = ClientSize.Height;
            prefs.Save(); LayoutMenuControls(); Invalidate();
        }
        private void DisplayChoice(Graphics g, string title, string value, string detail, float x, float y, float w, Action action, bool enabled = true)
        {
            Txt(g, title, x, y, 13, Palette.Text, true);
            AvailableButton(g, value + "  ›", x, y + 25, w, () => { action(); prefs.Save(); Invalidate(); }, enabled, detail, false, 30);
            Txt(g, detail, x, y + 62, 10, Palette.Muted, false, w);
        }
        private void DrawSettings(Graphics g)
        {
            Fill(g, Palette.Bg, 0, 0, VW, VH);
            Button(g, "← Назад", 48, 30, 150, () => OpenMenu(menuBack));
            Txt(g, "Экран и производительность", 48, 99, 32, Palette.Text, true);
            Txt(g, "Настройки применяются сразу и сохраняются на этом компьютере", 50, 151, 14, Palette.Muted);
            float x = 48, w = (VW - 136) / 3, x2 = x + w + 20, x3 = x2 + w + 20;
            Section(g, "ЭКРАН", x, 195, w); Section(g, "ГРАФИКА", x2, 195, w); Section(g, "ИГРА", x3, 195, w);
            SettingRow(g, "Полноэкранное окно", "Без рамки · разрешение монитора · " + prefs.KeyLabel("Fullscreen"), x, 230, w, fullScreen, ToggleFullScreen);
            DisplayChoice(g, "Разрешение окна", fullScreen ? System.Windows.Forms.Screen.FromControl(this).Bounds.Width + " × " + System.Windows.Forms.Screen.FromControl(this).Bounds.Height : ClientSize.Width + " × " + ClientSize.Height,
                fullScreen ? "В полном экране используется разрешение рабочего стола." : "1280×720 — 2560×1440, в пределах экрана", x, 345, w, CycleResolution, !fullScreen);
            DisplayChoice(g, "Ограничение кадров", prefs.FrameLimit == 0 ? "Без ограничения" : prefs.FrameLimit + " FPS", "Верхняя граница; в покое кадры не перерисовываются.", x, 452, w, () => { int[] limits = {30,60,90,120,0}; prefs.FrameLimit = limits[(Array.IndexOf(limits, prefs.FrameLimit) + 1) % limits.Length]; nextFrameAt = 0; });
            SettingRow(g, "Время кадра", "Показывать нагрузку отрисовки и частоту обновлений", x, 560, w, prefs.PerformanceOverlay, () => prefs.PerformanceOverlay = !prefs.PerformanceOverlay);
            DisplayChoice(g, "Качество карты", new[] {"Быстрое", "Сбалансированное", "Высокое"}[prefs.GraphicsQuality], "65 / 85 / 100% детализации; высокое — сглаживание", x2, 230, w, () => prefs.GraphicsQuality = (prefs.GraphicsQuality + 1) % 3);
            DisplayChoice(g, "Плотность подписей", new[] {"Без подписей", "Только страны", "Страны и области"}[prefs.Labels ? prefs.LabelDensity : 0], "Области появляются при достаточном приближении", x2, 345, w, () => { prefs.LabelDensity = ((prefs.Labels ? prefs.LabelDensity : 0) + 1) % 3; prefs.Labels = prefs.LabelDensity != 0; });
            SettingRow(g, "Границы областей", "Административные границы при приближении", x2, 452, w, prefs.RegionBorders, () => prefs.RegionBorders = !prefs.RegionBorders);
            SettingRow(g, "Текстура карты", "Тонкая фактура атласа", x2, 560, w, prefs.Texture, () => prefs.Texture = !prefs.Texture);
            SettingRow(g, "Подсказки при наведении", "Объяснения кнопок и показателей", x3, 230, w, prefs.Tooltips, () => prefs.Tooltips = !prefs.Tooltips);
            SettingRow(g, "Автосохранение", "Каждые 90 дней и при выходе из кампании", x3, 345, w, prefs.Autosave, () => prefs.Autosave = !prefs.Autosave);
            SettingRow(g, "Обучение в новой игре", "Короткая последовательность первых действий", x3, 452, w, prefs.Tutorial, () => prefs.Tutorial = !prefs.Tutorial);
            Button(g, "Настроить клавиши →", x3, 585, w, () => OpenMenu("controls"));
            Para(g, "Понижение качества уменьшает размер фонового изображения карты. Точность выбора областей и расчёты кампании сохраняются. Полноэкранный режим работает без рамки, без смены видеорежима монитора.", x, 701, VW - 96, 75, 14);
        }
    }
}
