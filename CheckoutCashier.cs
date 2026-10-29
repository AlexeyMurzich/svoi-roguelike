using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image cashierArt;
        readonly CashierAnimation cashierAnimation = new CashierAnimation();
        readonly CashierSound cashierSound = new CashierSound();
        public void StartTestCheckout()
        {
            selected = 3; OpenCheckout(); RefreshScene(); WakeMotion();
        }
        void ResetCashier()
        {
            cashierAnimation.Reset(); cashierSound.Stop();
        }
        void AdvanceCashier(float seconds)
        {
            if (!checkout || mapScreen || mimicratorPreview || GameplayPaused || !Motion.IsFinite(seconds) || seconds <= 0) return;
            bool started = cashierAnimation.Advance(seconds);
            if (started && Visible && ContainsFocus) cashierSound.Play();
        }
        void DrawCashierSprite(Graphics g, RectangleF area)
        {
            actorArtCache.Draw(g, cashierArt, area, new RectangleF(0, 0, cashierArt.Width, cashierArt.Height), 1, false);
        }
        void DrawCashier(Graphics g)
        {
            if (cashierArt == null) return;
            double time = cashierAnimation.PoseSeconds;
            // The desk remains planted; only a tiny breathing sway is visible.
            float sway = (float)Math.Sin(time * .82) * .9f;
            float breathe = (float)Math.Sin(time * 1.45) * 1.15f;
            var area = new RectangleF(0 + sway, 278 + breathe, 326, 391.2f);
            bool split = cashierAnimation.SplitFrame;
            int tick = (int)(cashierAnimation.GlitchAge * 25);
            var saved = g.Save();
            try
            {
                g.SetClip(new RectangleF(1, 224, 329, 469), CombineMode.Intersect);
                if (!split) { DrawCashierSprite(g, area); return; }
                // Replace narrow bands rather than duplicating the complete face or flashing the screen.
                var bands = new[] { new RectangleF(0, area.Y + 52, 330, 7),
                    new RectangleF(0, area.Y + 135, 330, 12), new RectangleF(0, area.Y + 246, 330, 8) };
                var layer = g.Save();
                foreach (var band in bands) g.SetClip(band, CombineMode.Exclude);
                DrawCashierSprite(g, area);
                g.Restore(layer);
                for (int i = 0; i < bands.Length; i++)
                {
                    var stripe = g.Save();
                    g.SetClip(bands[i], CombineMode.Intersect);
                    var shifted = area; shifted.X += ((tick + i) % 2 == 0 ? 1 : -1) * (3 + (tick * 3 + i) % 5);
                    DrawCashierSprite(g, shifted);
                    g.Restore(stripe);
                }
                using (var pen = new Pen(Color.FromArgb(130, 72, 224, 162), 1))
                    g.DrawLine(pen, 62, area.Y + 147, 119 + tick % 19, area.Y + 147);
            }
            finally { g.Restore(saved); }
        }
    }
}
