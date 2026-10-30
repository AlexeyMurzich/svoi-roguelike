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
        readonly Image[] negativeDoctorArt = new Image[3];
        int doctorRelicPage;
        int NegativeFights(Organ organ)
        {
            int remaining = currentHero.Doctors.RemainingFights(organ);
            if (combat != null && (combat.Phase == CombatPhase.Player || combat.Phase == CombatPhase.Enemy) &&
                (organ == Organ.Eye && combat.HiddenCosts || organ == Organ.Liver && combat.LiverPenaltyActive)) remaining++;
            return remaining;
        }
        string NegativeDescription(Organ organ)
        {
            if (organ == Organ.Heart) return "Максимум HP уменьшен на 25% (−" + currentHero.Doctors.HeartHealthLost + " HP) до конца забега.";
            int left = NegativeFights(organ);
            if (left == 0) return "Последствия операции прошли. Штраф больше не действует.";
            return (organ == Organ.Eye ? "Стоимость карт скрыта символом «?»." : "В начале боя получаете 5 яда.") + " Осталось боёв: " + left + (combat != null && (combat.Phase == CombatPhase.Player || combat.Phase == CombatPhase.Enemy) ? " (включая текущий)." : ".");
        }
        void DrawNegativeRelic(Graphics g, RectangleF area, Organ organ, bool expired)
        {
            int index = (int)organ;
            if (negativeDoctorArt[index] == null)
            {
                string id = organ == Organ.Eye ? "eye" : organ == Organ.Heart ? "heart" : "liver";
                negativeDoctorArt[index] = assetStore.Get("relics/lost-" + id + ".png", "relics.lost-" + id + ".png");
            }
            DrawDoctorRelicImage(g, negativeDoctorArt[index], area, expired);
        }
        void DrawDoctorRelic(Graphics g, RectangleF area, DoctorDrop item, bool gray)
        {
            int index = (int)item;
            if (acquiredDoctorArt[index] == null)
            {
                string id = item == DoctorDrop.Eye ? "eye" : item == DoctorDrop.Heart ? "heart" : item == DoctorDrop.Liver ? "liver" : "chocolate";
                acquiredDoctorArt[index] = assetStore.Get("relics/doctor-" + id + ".png", "relics.doctor-" + id + ".png");
            }
            DrawDoctorRelicImage(g, acquiredDoctorArt[index], area, gray);
        }
        void DrawDoctorRelicImage(Graphics g, Image art, RectangleF area, bool gray)
        {
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
                    if (visit.Choice == DoctorChoice.Treatment)
                    {
                        DrawNegativeRelic(canvas, new RectangleF(394, 418, 62, 62), visit.LostOrgan, false);
                        TextAt(canvas, NegativeDescription(visit.LostOrgan), 476, 421, 16, Color.Salmon, 400);
                    }
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
            foreach (var organ in currentHero.Doctors.LostOrgans)
            {
                var area = new RectangleF(86 + index++ * 49, 73, 40, 40);
                int left = NegativeFights(organ);
                bool expired = organ != Organ.Heart && left == 0;
                DrawNegativeRelic(g, area, organ, expired);
                Rect(g, area.X, area.Bottom - 8, 40, 17, Color.FromArgb(220, 24, 12, 20));
                TextAt(g, organ == Organ.Heart ? "−25%" : left.ToString(), area.X + 3, area.Bottom - 8, 12, expired ? muted : Color.Salmon, 38, true);
                actions.Add(Tuple.Create(new RectangleF(area.X, area.Y, 40, 49), (Action)(() => SetInventory("relics"))));
            }
        }
        void DrawDoctorInventory(Graphics g)
        {
            var start = currentHero.Effects.Chosen;
            FitImage(g, relicImages[start.Icon], new RectangleF(247, 269, 64, 64));
            TextAt(g, start.Name + " · " + start.Description, 332, 269, 16, text, 690);
            int positives = currentHero.Doctors.Items.Count;
            int count = positives + currentHero.Doctors.LostOrgans.Count;
            const int pageSize = 4;
            int maxPage = Math.Max(0, (count - 1) / pageSize);
            doctorRelicPage = Math.Min(doctorRelicPage, maxPage);
            int end = Math.Min(count, (doctorRelicPage + 1) * pageSize);
            for (int at = doctorRelicPage * pageSize; at < end; at++)
            {
                float y = 358 + (at % pageSize) * 70;
                if (at < positives)
                {
                    var item = currentHero.Doctors.Items[at];
                    bool used = item == DoctorDrop.Heart && currentHero.Doctors.HeartUsed;
                    DrawDoctorRelic(g, new RectangleF(257, y, 45, 45), item, used);
                    TextAt(g, DoctorRules.Name(item), 332, y, 19, used ? muted : gold, 650, true);
                    TextAt(g, DoctorRules.Description(item, used), 332, y + 26, 14, text, 690);
                }
                else
                {
                    var organ = currentHero.Doctors.LostOrgans[at - positives];
                    bool expired = organ != Organ.Heart && NegativeFights(organ) == 0;
                    DrawNegativeRelic(g, new RectangleF(257, y, 45, 45), organ, expired);
                    TextAt(g, DoctorRules.NegativeName(organ) + " · негативная реликвия", 332, y, 19, expired ? muted : Color.Salmon, 680, true);
                    TextAt(g, NegativeDescription(organ), 332, y + 26, 14, text, 690);
                }
            }
            if (maxPage > 0)
            {
                if (doctorRelicPage > 0) Button(g, "← Назад", 237, 687, 155, 34, () => doctorRelicPage--);
                TextAt(g, (doctorRelicPage + 1) + " / " + (maxPage + 1), 606, 695, 12, muted, 130);
                if (doctorRelicPage < maxPage) Button(g, "Далее →", 887, 687, 155, 34, () => doctorRelicPage++);
            }
        }
    }
}
