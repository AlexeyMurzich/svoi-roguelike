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
            1033,326,1044,330,1051,321,1058,332,1077,326,1072,337,1094,331,1086,341,1109,341,1098,351,1108,357,1096,363,
            1097,386,1090,399,1106,406,1123,415,1142,421,1156,416,1166,404,1174,401,1180,391,
            1265,264,1274,257,1291,260,1297,268,1288,281,1195,408,1191,420,1180,438,1170,449,
            1148,447,1131,435,1121,455,1119,483,1128,507,1147,529,1163,554,1162,572,1178,598,1191,605,
            1208,600,1217,599,1222,607,1215,621,1205,625,1206,637,1199,647,1188,649,1181,641,
            1082,649,1072,653,1041,653,1035,647,1024,646,1009,651,987,650,977,645,978,635,
            981,611,976,598,983,571,987,551,983,533,992,507,984,491,972,494,961,506,951,508,944,521,939,511,
            930,516,929,504,923,502,932,485,947,474,960,457,966,437,980,418,994,402,1014,394,1024,386,1025,362,1014,354,1024,348,1019,338,1030,340 });
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
            RectangleF source = master ? new RectangleF(919, 254, 383, 401) : new RectangleF(716, 448, 197, 204);
            var target = SkaterArea(index);
            float factor = Math.Min(target.Width / source.Width, target.Height / source.Height);
            target = new RectangleF(target.X + (target.Width - source.Width * factor) / 2, target.Bottom - source.Height * factor, source.Width * factor, source.Height * factor);
            float move = EnemyIsAnimating(index) ? -30 * (float)Math.Sin(Math.Min(1, attackTimeline.Time / attackTimeline.Duration) * Math.PI) : 0;
            target.Offset(move, 0);
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
