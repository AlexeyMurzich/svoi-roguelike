using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image hospitalArt, femaleDoctorArt, maleDoctorArt;
        Image poisonStatusArt, bleedingStatusArt;
        readonly Image[] acquiredDoctorArt = new Image[4];
        void DrawDoctorRelic(Graphics g, RectangleF area, DoctorDrop item, bool gray)
        {
            int index = (int)item;
            if (acquiredDoctorArt[index] == null)
            {
                string id = item == DoctorDrop.Eye ? "eye" : item == DoctorDrop.Heart ? "heart" : item == DoctorDrop.Liver ? "liver" : "chocolate";
                acquiredDoctorArt[index] = assetStore.Get("relics/doctor-" + id + ".png", "relics.doctor-" + id + ".png");
            }
            var art = acquiredDoctorArt[index];
            if (!gray) DrawInterfaceImage(g, art, area, new RectangleF(0, 0, art.Width, art.Height));
            else
            {
                // No bitmap clone: apply grayscale to the shared owned source.
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(new ColorMatrix(new[] {
                        new[] { .2093f, .2093f, .2093f, 0f, 0f },
                        new[] { .4109f, .4109f, .4109f, 0f, 0f },
                        new[] { .0798f, .0798f, .0798f, 0f, 0f },
                        new[] { 0f, 0f, 0f, 1f, 0f }, new[] { 0f, 0f, 0f, 0f, 1f } }));
                    g.DrawImage(art, new[] { new PointF(area.Left, area.Top), new PointF(area.Right, area.Top), new PointF(area.Left, area.Bottom) }, new RectangleF(0, 0, art.Width, art.Height), GraphicsUnit.Pixel, attributes);
                }
            }
        }
        void DrawDoctorStatusIcon(Graphics g, int icon, RectangleF area)
        {
            Image art;
            if (icon == 22)
            {
                if (poisonStatusArt == null) poisonStatusArt = assetStore.Get("statuses/poison.png", "statuses.poison.png");
                art = poisonStatusArt;
            }
            else
            {
                if (bleedingStatusArt == null) bleedingStatusArt = assetStore.Get("statuses/bleeding.png", "statuses.bleeding.png");
                art = bleedingStatusArt;
            }
            DrawInterfaceImage(g, art, area, new RectangleF(0, 0, art.Width, art.Height));
        }
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
                DrawDoctorRelic(g, area, item, item == DoctorDrop.Heart && currentHero.Doctors.HeartUsed);
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
                DrawDoctorRelic(g, new RectangleF(257, y, 45, 45), item, used);
                TextAt(g, DoctorRules.Name(item), 332, y, 19, used ? muted : gold, 650, true);
                TextAt(g, DoctorRules.Description(item, used), 332, y + 26, 14, text, 690);
            }
        }
    }
}
