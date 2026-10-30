using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    // Draws small distinct status/relic symbols without owning source bitmaps.
    public static class DoctorIcons
    {
        public static void Draw(Graphics g, RectangleF area, int kind, bool gray)
        {
            var state = g.Save();
            try
            {
                g.TranslateTransform(area.X, area.Y);
                g.ScaleTransform(area.Width / 40, area.Height / 40);
                Color tone = gray ? Color.Gray : kind == 4 ? Color.YellowGreen : kind == 3 ? Color.SaddleBrown : Color.Crimson;
                using (var fill = new SolidBrush(tone))
                using (var edge = new Pen(gray ? Color.DarkGray : Color.Wheat, 2))
                using (var white = new SolidBrush(gray ? Color.Silver : Color.Ivory))
                {
                    if (kind == 0)
                    {
                        g.FillEllipse(white, 2, 10, 36, 20); g.DrawEllipse(edge, 2, 10, 36, 20);
                        g.FillEllipse(fill, 13, 11, 15, 18);
                        using (var pupil = new SolidBrush(Color.Black)) g.FillEllipse(pupil, 18, 15, 5, 10);
                    }
                    else if (kind == 1)
                    {
                        using (var heart = new GraphicsPath())
                        {
                            heart.AddBezier(20, 35, -12, 15, 7, -3, 20, 11);
                            heart.AddBezier(20, 11, 33, -3, 52, 15, 20, 35);
                            g.FillPath(fill, heart); g.DrawPath(edge, heart);
                        }
                    }
                    else if (kind == 2)
                    {
                        var points = new[] { new PointF(3, 14), new PointF(22, 7), new PointF(37, 13), new PointF(30, 29), new PointF(10, 34) };
                        g.FillPolygon(fill, points); g.DrawPolygon(edge, points); g.DrawLine(edge, 22, 9, 18, 26);
                    }
                    else if (kind == 3)
                    {
                        g.FillRectangle(fill, 5, 4, 30, 32); g.DrawRectangle(edge, 5, 4, 30, 32);
                        for (int i = 1; i < 3; i++) { g.DrawLine(edge, 5 + i * 10, 5, 5 + i * 10, 35); g.DrawLine(edge, 5, 5 + i * 10, 35, 5 + i * 10); }
                    }
                    else if (kind == 4)
                    {
                        g.FillEllipse(fill, 5, 5, 30, 25); g.DrawEllipse(edge, 5, 5, 30, 25);
                        using (var dark = new SolidBrush(Color.DarkGreen)) { g.FillEllipse(dark, 11, 12, 6, 7); g.FillEllipse(dark, 23, 12, 6, 7); }
                        g.DrawLine(edge, 10, 34, 31, 34);
                    }
                    else
                    {
                        // Blood drop with a bandage slash, distinct from poison's skull.
                        using (var drop = new GraphicsPath())
                        {
                            drop.AddBezier(20, 2, 13, 14, 1, 20, 8, 32);
                            drop.AddBezier(8, 32, 16, 43, 43, 37, 31, 18); drop.CloseFigure();
                            g.FillPath(fill, drop); g.DrawPath(edge, drop);
                        }
                        g.DrawLine(edge, 15, 29, 26, 20);
                    }
                }
            }
            finally { g.Restore(state); }
        }
    }

    public sealed partial class Game
    {
        Image hospitalArt, femaleDoctorArt, maleDoctorArt;
        bool IsDoctorQuestion
        {
            get { return mapRun != null && mapRun.Pending != null && mapRun.Pending.Kind == RoomKind.Question && mapRun.QuestionAt(mapRun.Pending.Id, currentHero) == QuestionKind.Doctors; }
        }
        bool QuestionBattle
        {
            get
            {
                if (mapRun == null || mapRun.Pending == null || mapRun.Pending.Kind != RoomKind.Question || mapRun.PendingIsRevisit) return false;
                return IsDoctorQuestion ? mapRun.DoctorsAt(mapRun.Pending.Id).Choice == DoctorChoice.Battle ||
                    mapRun.DoctorsAt(mapRun.Pending.Id).Choice == DoctorChoice.Victory && combat != null : mapRun.QuestionAt(mapRun.Pending.Id, currentHero) == QuestionKind.Fight;
            }
        }
        EnemyKind QuestionEnemy { get { return IsDoctorQuestion ? EnemyKind.Doctors : mapRun.StartQuestionFight(mapRun.Pending.Id); } }
        bool CanCompleteQuestion
        {
            get
            {
                if (!IsDoctorQuestion || mapRun.PendingIsRevisit) return true;
                var choice = mapRun.DoctorsAt(mapRun.Pending.Id).Choice;
                return choice == DoctorChoice.Treatment || choice == DoctorChoice.Victory;
            }
        }
        string QuestionIdentity(int id)
        {
            var kind = mapRun.QuestionAt(id, currentHero);
            return kind == QuestionKind.Doctors ? "Безумные доктора" : kind == QuestionKind.Fight ? "Обычный бой" : "Тихая комната";
        }
        void LoadDoctorArt()
        {
            if (hospitalArt == null) hospitalArt = assetStore.Get("combat-hospital.png", "combat-hospital.png");
            if (femaleDoctorArt == null) femaleDoctorArt = assetStore.Get("enemies/doctors/female.png", "enemies.doctors.female.png");
            if (maleDoctorArt == null) maleDoctorArt = assetStore.Get("enemies/doctors/male.png", "enemies.doctors.male.png");
        }
        void DrawHospital(Graphics g)
        {
            LoadDoctorArt();
            float seam = CombatFloor - 65;
            var wall = new RectangleF(0, 64, CanvasWidth, Math.Max(1, seam - 64));
            var floor = new RectangleF(0, seam, CanvasWidth, CanvasHeight - seam);
            float sourceSeam = hospitalArt.Height * .72f;
            DrawInterfaceImage(g, hospitalArt, wall, new RectangleF(0, 0, hospitalArt.Width, sourceSeam));
            DrawInterfaceImage(g, hospitalArt, floor, new RectangleF(0, sourceSeam, hospitalArt.Width, hospitalArt.Height - sourceSeam));
            if (combat != null && combat.Enemy == EnemyKind.Doctors)
                for (int i = 0; i < combat.Enemies.Count; i++)
                    if (combat.Enemies[i].IsAlive) SoftGroundShadow(g, new RectangleF(EnemyCenter(i) - 70, CombatFloor - 6, 140, 18), 110);
        }
        void DoctorPose(Graphics g, int index, int pose, RectangleF area)
        {
            LoadDoctorArt();
            var art = index == 0 ? femaleDoctorArt : maleDoctorArt;
            // The approved male sheet has unequal pose extents. Explicit crops prevent
            // an attack shoe leaking into idle or the extended foot being cut in half.
            var source = index == 1 ? (pose == 0 ? new RectangleF(0, 0, art.Width * 640f / 1470f, art.Height) : new RectangleF(art.Width * 650f / 1470f, 0, art.Width * 820f / 1470f, art.Height)) :
                pose == 0 ? new RectangleF(0, 0, art.Width * 780f / 1672f, art.Height) : new RectangleF(art.Width * 780f / 1672f, 0, art.Width * 892f / 1672f, art.Height);
            float scale = Math.Min(area.Width / source.Width, area.Height / art.Height);
            var target = new RectangleF(area.X + (area.Width - source.Width * scale) / 2, area.Bottom - art.Height * scale, source.Width * scale, art.Height * scale);
            actorArtCache.Draw(g, art, target, source, 1, false);
        }
        void DrawDoctorSprite(Graphics g, int index)
        {
            var area = EnemyArea(index);
            bool attacking = EnemyIsAnimating(index);
            if (attacking) area.Offset(BiteOffset(attackTimeline.Time), 0);
            DoctorPose(g, index, attacking && attackTimeline.Time >= .2f && attackTimeline.Time < .72f ? 1 : 0, area);
        }
        void RewardDoctors()
        {
            if (combat == null || combat.Enemy != EnemyKind.Doctors || !IsDoctorQuestion) return;
            var visit = mapRun.DoctorsAt(mapRun.Pending.Id);
            if (visit.Reward(combat)) combatNotice = "Победа · " + DoctorRules.Name(visit.Drop) + " · здоровье восстановлено";
        }
        internal void StartTestDoctorEvent()
        {
            ReturnToMainMenu(); selected = 3; OpenCheckout(); TakeBuff(); doctorTestSeed = 42; OpenActMap(); doctorTestSeed = null;
            foreach (var node in mapRun.Map.Nodes)
                if (node.Kind == RoomKind.Question && mapRun.QuestionAt(node.Id, currentHero) == QuestionKind.Doctors)
                {
                    mapRun.EnterDoctorTestRoom(node.Id); RefreshScene(); return;
                }
            throw new InvalidOperationException("No doctors in this test seed.");
        }
        void DrawDoctorEvent(Graphics g)
        {
            DrawHospital(g);
            var visit = mapRun.DoctorsAt(mapRun.Pending.Id);
            DoctorPose(g, 0, 0, new RectangleF(CanvasWidth * .06f, 145, 300, CanvasHeight - 220));
            DoctorPose(g, 1, 0, new RectangleF(CanvasWidth - 360, 145, 300, CanvasHeight - 220));
            DrawRunBar(g); DrawMapRelics(g);
            DrawCenteredOverlay(g, canvas =>
            {
                RelicFrame(canvas, 365, 220, 550, 435, true);
                TextAt(canvas, "Безумные доктора", 390, 245, 29, gold, 500, true);
                if (visit.Choice == DoctorChoice.None)
                {
                    TextAt(canvas, "«Вылечим полностью. Один орган — наша плата».", 392, 300, 19, text, 492);
                    TextAt(canvas, "Случайно потеряете глаз (цены скрыты на 3 боя), сердце (−25% максимума HP) или печень (яд 5 в начале 3 боёв).", 392, 365, 16, text, 492);
                    Button(canvas, "Лечение · отдать случайный орган", 390, 483, 500, 53, () => { visit.Treat(currentHero); RefreshScene(); });
                    Button(canvas, "В бой · полный хил и случайный дроп", 390, 555, 500, 53, () => { if (visit.Fight()) { EnsureCombat(); RefreshScene(); } });
                }
                else
                {
                    string result = visit.Choice == DoctorChoice.Treatment ? "Здоровье восстановлено. Потерян орган: " + DoctorRules.Name((DoctorDrop)visit.LostOrgan) : "Получено: " + DoctorRules.Name(visit.Drop) + ". Здоровье восстановлено.";
                    TextAt(canvas, result, 392, 327, 23, text, 492, true);
                    Button(canvas, "Продолжить путь", 390, 555, 500, 53, CompleteMapRoom, true);
                }
            });
            if (inventoryView != "") DrawRunInventory(g);
        }
        void DrawAcquiredDoctorRelics(Graphics g)
        {
            int index = 0;
            foreach (var item in currentHero.Doctors.Items)
            {
                var area = new RectangleF(86 + index++ * 49, 73, 40, 40);
                DoctorIcons.Draw(g, area, (int)item, item == DoctorDrop.Heart && currentHero.Doctors.HeartUsed);
                actions.Add(Tuple.Create(area, (Action)(() => SetInventory("relics"))));
            }
        }
        void DrawDoctorInventory(Graphics g)
        {
            var start = currentHero.Effects.Chosen;
            FitImage(g, relicImages[start.Icon], new RectangleF(247, 269, 64, 64));
            TextAt(g, start.Name + " · " + start.Description, 332, 269, 16, text, 690);
            string penalties = "";
            if (currentHero.Doctors.BlindFights > 0) penalties += "Глаз: цены скрыты ещё на " + currentHero.Doctors.BlindFights + " боя. ";
            if (currentHero.Doctors.LiverFights > 0) penalties += "Печень: яд 5 ещё на " + currentHero.Doctors.LiverFights + " боя.";
            if (penalties.Length > 0) TextAt(g, penalties, 247, 672, 15, gold, 775);
            int row = 0;
            foreach (var item in currentHero.Doctors.Items)
            {
                float y = 358 + row++ * 70;
                bool used = item == DoctorDrop.Heart && currentHero.Doctors.HeartUsed;
                DoctorIcons.Draw(g, new RectangleF(257, y, 45, 45), (int)item, used);
                TextAt(g, DoctorRules.Name(item), 332, y, 19, used ? muted : gold, 650, true);
                TextAt(g, DoctorRules.Description(item, used), 332, y + 26, 14, text, 690);
            }
        }
    }
}
