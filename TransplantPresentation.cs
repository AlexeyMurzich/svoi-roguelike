using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        bool DoctorRewardOpen
        {
            get { return IsDoctorQuestion && combat != null && combat.Phase == CombatPhase.Victory && !CombatBusy; }
        }
        void ClaimDoctorReward()
        {
            if (DoctorRewardOpen && mapRun.DoctorsAt(mapRun.Pending.Id).ClaimReward(combat)) RefreshScene();
        }
        void TransplantTitle(Graphics g, string label, float y, float size)
        {
            using (var format = new StringFormat { Alignment = StringAlignment.Center })
            using (var shadow = new SolidBrush(Color.FromArgb(210, 0, 0, 0)))
            using (var ink = new SolidBrush(gold))
            {
                var font = HudFont(size, true);
                g.DrawString(label, font, shadow, new RectangleF(330, y + 3, 626, 130), format);
                g.DrawString(label, font, ink, new RectangleF(327, y, 626, 130), format);
            }
        }
        void TransplantPanel(Graphics g, float top, float height)
        {
            Rect(g, 314, top + 12, 664, height, Color.FromArgb(110, 0, 0, 0));
            var panel = new RectangleF(310, top, 660, height);
            using (var fill = new LinearGradientBrush(panel, Color.FromArgb(249, 15, 30, 31), Color.FromArgb(250, 7, 13, 19), 90)) g.FillRectangle(fill, panel);
            Stroke(g, 310, top, 660, height, Color.FromArgb(170, gold));
            Stroke(g, 319, top + 9, 642, height - 18, Color.FromArgb(50, gold));
            using (var pen = new Pen(gold, 3))
            {
                foreach (float x in new[] { 310f, 970f }) foreach (float y in new[] { top, top + height })
                {
                    g.DrawLine(pen, x, y, x + (x == 310 ? 34 : -34), y);
                    g.DrawLine(pen, x, y, x, y + (y == top ? 27 : -27));
                }
            }
            // Gold incision motif; no extra asset or per-frame bitmap.
            using (var pen = new Pen(Color.FromArgb(160, gold), 1.5f))
            {
                g.DrawLine(pen, 457, top + 35, 604, top + 35);
                g.DrawLine(pen, 676, top + 35, 823, top + 35);
                g.DrawPolygon(pen, new[] { new PointF(640, top + 20), new PointF(656, top + 35), new PointF(640, top + 50), new PointF(624, top + 35) });
            }
        }
        void TransplantAction(Graphics g, string label, float y, bool danger, Action action)
        {
            var area = new RectangleF(360, y, 560, 76);
            using (var fill = new LinearGradientBrush(area, danger ? Color.FromArgb(86, 30, 37) : Color.FromArgb(25, 65, 62), danger ? Color.FromArgb(34, 15, 23) : Color.FromArgb(12, 32, 36), 0f)) g.FillRectangle(fill, area);
            Stroke(g, area.X, area.Y, area.Width, area.Height, danger ? Color.FromArgb(158, 77, 74) : Color.FromArgb(138, gold));
            Rect(g, area.X, area.Y, 4, area.Height, danger ? Color.FromArgb(191, 70, 70) : gold);
            TextAt(g, label, 383, y + 23, 19, text, 475, true);
            TextAt(g, "›", 875, y + 12, 32, gold, 35, true);
            actions.Add(Tuple.Create(area, action));
        }
        void DrawTransplantEvent(Graphics g)
        {
            DrawHospital(g);
            Rect(g, 0, 64, CanvasWidth, CanvasHeight - 64, Color.FromArgb(95, 2, 11, 18));
            DrawCenteredOverlay(g, canvas => {
                DoctorPose(canvas, 0, 0, new RectangleF(18, 150, 300, 650));
                DoctorPose(canvas, 1, 0, new RectangleF(962, 150, 300, 650));
            });
            DrawRunBar(g); DrawMapRelics(g);
            var visit = mapRun.DoctorsAt(mapRun.Pending.Id);
            DrawCenteredOverlay(g, canvas =>
            {
                TransplantPanel(canvas, 195, 472);
                if (visit.Choice == DoctorChoice.None)
                {
                    TransplantTitle(canvas, "ЧЁРНЫЕ", 270, 32);
                    TransplantTitle(canvas, "ТРАНСПЛАНТОЛОГИ", 315, 33);
                    TransplantAction(canvas, "Полное лечение и потеря 1 органа", 444, false, () => { visit.Treat(currentHero); RefreshScene(); });
                    TransplantAction(canvas, "В бой", 543, true, () => { if (visit.Fight()) { EnsureCombat(); RefreshScene(); } });
                }
                else
                {
                    TransplantTitle(canvas, "ОПЕРАЦИЯ ЗАВЕРШЕНА", 270, 28);
                    DrawNegativeRelic(canvas, new RectangleF(588, 338, 104, 104), visit.LostOrgan, false);
                    TransplantTitle(canvas, DoctorRules.NegativeName(visit.LostOrgan), 453, 23);
                    TextAt(canvas, "Здоровье полностью восстановлено", 423, 507, 18, text, 465);
                    TransplantAction(canvas, "Продолжить путь", 563, false, CompleteMapRoom);
                }
            });
            if (inventoryView != "") DrawRunInventory(g);
        }
        void DrawDoctorRewards(Graphics g)
        {
            // Modal: underlying battle, cards and route button cannot receive clicks.
            actions.Clear(); combatHintAreas.Clear();
            Rect(g, 0, 64, CanvasWidth, CanvasHeight - 64, Color.FromArgb(210, 4, 10, 17));
            var visit = mapRun.DoctorsAt(mapRun.Pending.Id);
            DrawCenteredOverlay(g, canvas =>
            {
                TransplantPanel(canvas, 155, 550);
                TransplantTitle(canvas, "ПОБЕДА", 218, 39);
                TransplantTitle(canvas, "НАГРАДЫ", 276, 19);
                Rect(canvas, 358, 335, 564, 73, Color.FromArgb(27, 53, 46));
                FitImage(canvas, healthIcon, new RectangleF(379, 348, 46, 46));
                TextAt(canvas, "Полное восстановление здоровья", 447, 348, 18, text, 430, true);
                TextAt(canvas, currentHero.Health + " / " + currentHero.MaxHealth + " HP · восстановлено", 447, 376, 14, Color.FromArgb(133, 190, 155), 430);
                var item = new RectangleF(358, 425, 564, 130);
                using (var fill = new LinearGradientBrush(item, Color.FromArgb(45, 48, 43), Color.FromArgb(18, 29, 32), 0f)) canvas.FillRectangle(fill, item);
                Stroke(canvas, item.X, item.Y, item.Width, item.Height, visit.RewardClaimed ? muted : gold);
                DrawDoctorRelic(canvas, new RectangleF(378, 448, 80, 80), visit.Drop, visit.RewardClaimed);
                TextAt(canvas, DoctorRules.Name(visit.Drop), 479, 438, 23, gold, 410, true);
                TextAt(canvas, DoctorRules.Description(visit.Drop, false), 479, 475, 15, text, 405);
                TextAt(canvas, visit.RewardClaimed ? "Получено" : "Забрать", 786, 531, 13, visit.RewardClaimed ? muted : gold, 110, true);
                if (!visit.RewardClaimed) actions.Add(Tuple.Create(item, (Action)ClaimDoctorReward));
                if (visit.RewardClaimed) TransplantAction(canvas, "Продолжить путь", 591, false, CompleteMapRoom);
            });
        }
    }
}
