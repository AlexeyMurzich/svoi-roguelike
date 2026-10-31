using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image skaterApprovedArt;
        // Source vertices isolate the approved sprites. No generative redraw, including faces.
        static readonly PointF[] MasterOutline = Points(new float[] {
            1031,333,1034,342,1040,339,1044,329,1055,325,1050,336,1057,329,1077,320,1075,328,1081,322,1080,331,
            1095,332,1092,337,1104,337,1098,340,1110,344,1104,349,1096,351,1108,357,1101,358,1110,364,
            1100,361,1105,373,1099,370,1103,377,1098,376,1096,383,
            1097,386,1090,399,1102,407,1109,417,1117,419,1120,429,1128,432,1136,426,1145,428,1152,425,1158,423,
            1165,414,1174,412,1181,401,1183,395,1193,381,1267,267,1275,259,1284,258,1293,264,1295,270,1290,280,
            1200,403,1195,413,1185,423,1186,426,1181,432,1181,437,1176,441,1177,445,1171,449,1164,449,
            1157,460,1144,466,1133,466,1113,456,1111,475,1119,483,1128,496,1121,505,1114,510,
            1140,527,1148,539,1148,550,1155,566,1162,579,1169,588,1163,597,1173,604,1182,612,
            1195,605,1210,599,1218,601,1220,606,1217,613,1211,620,1197,626,1193,628,1198,632,1198,640,
            1192,647,1185,650,1177,646,1174,639,1176,632,1080,650,1079,653,1038,653,1036,649,
            1027,650,1015,650,1002,652,990,649,979,645,979,636,981,611,976,598,983,571,987,551,983,533,
            992,507,984,491,982,481,972,480,966,490,970,502,971,507,966,510,961,506,957,501,
            946,511,944,518,939,518,938,511,934,515,929,515,929,509,925,511,922,507,925,501,
            922,501,928,491,938,481,945,474,960,457,966,437,980,418,994,402,1014,394,1024,386,
            1023,376,1020,367,1013,370,1016,361,1020,356,1012,354,1012,351,1029,346,1023,342,1031,342 });
        static readonly PointF[] PadawanOutline = Points(new float[] {
            786,458,806,451,828,455,842,469,848,490,841,502,856,507,862,523,869,539,863,552,
            871,564,884,586,880,600,894,612,902,611,909,616,905,629,893,633,891,643,881,649,871,642,
            785,643,777,649,766,644,765,636,752,634,741,628,740,620,748,615,758,610,759,591,
            766,571,771,555,766,542,753,537,742,530,735,529,730,534,720,530,719,519,727,513,
            750,511,766,502,774,493,771,480 });
        static PointF[] Points(float[] xy)
        {
            var result = new PointF[xy.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            return result;
        }
        internal void StartTestSkaters() { StartTestEncounter(EnemyKind.SkaterMaster); }
        RectangleF SkaterArea(int index)
        {
            float width = index == 0 ? 340 : 172, height = index == 0 ? 350 : 185;
            return new RectangleF(EnemyCenter(index) - width / 2, CombatFloor - height, width, height);
        }
        void DrawSkaterSprite(Graphics g, int index)
        {
            if (skaterApprovedArt == null) skaterApprovedArt = assetStore.Get("enemies/skaters/approved-source.png", "enemies.skaters.approved-source.png");
            bool master = index == 0;
            RectangleF source = master ? new RectangleF(919, 254, 383, 417) : new RectangleF(716, 448, 197, 204);
            var target = SkaterArea(index);
            float factor = Math.Min(target.Width / source.Width, target.Height / source.Height);
            target = new RectangleF(target.X + (target.Width - source.Width * factor) / 2, target.Bottom - source.Height * factor, source.Width * factor, source.Height * factor);
            float move = EnemyIsAnimating(index) ? -30 * (float)Math.Sin(Math.Min(1, attackTimeline.Time / attackTimeline.Duration) * Math.PI) : 0;
            target.Offset(move, 0);
            if (master)
            {
                // The approved mockup's health bar hid the rear wheel's bottom.
                // Reuse the intact wheel from that same approved raster, behind the board.
                var wheel = new RectangleF(target.X + (1036 - source.X) * factor,
                    target.Y + (645 - source.Y) * factor, 24 * factor, 24 * factor);
                using (var wheelClip = new GraphicsPath())
                {
                    wheelClip.AddEllipse(wheel);
                    var savedWheel = g.Save();
                    try
                    {
                        g.SetClip(wheelClip, CombineMode.Intersect);
                        actorArtCache.Draw(g, skaterApprovedArt, wheel, new RectangleF(1174, 626, 24, 24), 1, false);
                    }
                    finally { g.Restore(savedWheel); }
                }
            }
            using (var path = new GraphicsPath())
            {
                var outline = master ? MasterOutline : PadawanOutline;
                var mapped = new PointF[outline.Length];
                for (int i = 0; i < mapped.Length; i++) mapped[i] = new PointF(target.X + (outline[i].X - source.X) * factor, target.Y + (outline[i].Y - source.Y) * factor);
                path.AddPolygon(mapped);
                var saved = g.Save();
                try { g.SetClip(path, CombineMode.Intersect); actorArtCache.Draw(g, skaterApprovedArt, target, source, 1, false); }
                finally { g.Restore(saved); }
            }
        }
        void StartSkaterAttack(CombatEnemy actor)
        {
            bool master = actor.Kind == EnemyKind.SkaterMaster;
            if (Visible && ContainsFocus && !GameplayPaused) skaterSounds.Play(actor.Id, false);
            combatNotice = actor.Name + (master ? " · красный меч" : " · бластер");
            StartCombatAnimation(master ? AttackAnimation.SkaterSlash : AttackAnimation.PadawanShot, () =>
            {
                combat.ResolveNextEnemyAction(); SyncDeaths();
                combatNotice = combat.LastDamage == 0 ? "Удар заблокирован" : "Получено урона: " + combat.LastDamage;
            });
        }
        void DrawSkaterAttack(Graphics g)
        {
            bool master = attackTimeline.Kind == AttackAnimation.SkaterSlash;
            if (!master && attackTimeline.Kind != AttackAnimation.PadawanShot || actingEnemyIndex < 0) return;
            float time = attackTimeline.Time;
            if (time < .18f || time > .69f) return;
            float flight = Math.Max(0, Math.Min(1, (time - .18f) / (attackTimeline.HitTime - .18f)));
            float from = EnemyCenter(actingEnemyIndex) - 60, to = HeroSpriteCenter + 40;
            float x = from + (to - from) * flight, y = CombatFloor - (master ? 220 : 110) + (master ? 40 : -65) * flight;
            if (master) y -= 35 * (float)Math.Sin(flight * Math.PI);
            using (var glow = new Pen(Color.FromArgb(75, 255, 20, 30), master ? 17 : 13))
            using (var red = new Pen(Color.FromArgb(240, 255, 35, 35), master ? 6 : 5))
            using (var core = new Pen(Color.FromArgb(255, 255, 220, 210), 2))
            {
                if (master)
                {
                    g.DrawArc(glow, x - 40, y - 65, 85, 120, -65, 115);
                    g.DrawArc(red, x - 40, y - 65, 85, 120, -65, 115);
                    g.DrawArc(core, x - 40, y - 65, 85, 120, -65, 115);
                }
                else { g.DrawLine(glow, x, y, x + 38, y); g.DrawLine(red, x, y, x + 38, y); g.DrawLine(core, x, y, x + 38, y); }
            }
        }
    }
}
