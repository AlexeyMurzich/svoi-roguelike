using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        void DrawMainMenu(Graphics g)
        {
            TextAt(g, "С / Л", 36, 24, 24, gold, 100, true);
            TextAt(g, "СВОИ ЛЮДИ", 140, 31, 15, text, 320, true);
            TextAt(g, "СМОЛЕВИЧИ  /  " + GameVersion.Number, 1000, 32, 12, muted, 245);
            Rect(g, 36, 73, 1208, 1, Color.FromArgb(50, 58, 65));
            if (preview)
            {
                TextAt(g, names[selected], 40, 102, 34, colors[selected], 700, true);
                Portrait(g, selected, 240, 160, 800, 615);
                Button(g, "← К выбору персонажа", 940, 102, 300, 50, () => preview = false);
                TextAt(g, "← →  другой персонаж      Esc  назад", 40, 805, 12, muted, 1100);
                return;
            }

            TextAt(g, "СВОИ ЛЮДИ. СВОИ ПРАВИЛА.", 36, 104, 12, gold, 800, true);
            TextAt(g, "Кто сегодня в пати?", 32, 129, 42, text, 1100, true);
            TextAt(g, "Четыре персонажа. Четыре способа устроить хаос.", 36, 191, 17, muted, 1160);
            for (int i = 0; i < 4; i++)
            {
                int id = i;
                float x = 36 + i * 188;
                bool active = selected == i;
                Rect(g, x, 246, 173, 390, active ? Color.FromArgb(35, 44, 54) : surface);
                if (active)
                    Stroke(g, x, 246, 173, 390, colors[i]);
                Rect(g, x, 246, 173, 3, colors[i]);
                TextAt(g, "0" + (i + 1), x + 15, 260, 12, muted, 80);
                Portrait(g, i, x + 5, 289, 163, 210);
                TextAt(g, names[i], x + 15, 512, 25, active ? colors[i] : text, 150, true);
                TextAt(g, summaries[i], x + 15, 555, 14, muted, 148);
                actions.Add(Tuple.Create(new RectangleF(x, 246, 173, 390), (Action)(() =>
                {
                    ChooseMenuHero(id);
                    notice = "";
                })));
            }

            Rect(g, 812, 246, 432, 482, surface);
            Rect(g, 812, 246, 432, 3, colors[selected]);
            TextAt(g, tags[selected], 836, 269, 11, colors[selected], 383, true);
            TextAt(g, names[selected], 832, 300, 34, text, 385, true);
            TextAt(g, descriptions[selected], 836, 355, 16, text, 380);
            var r = rules[selected];
            for (int j = 0; j < 3; j++)
            {
                float yy = 501 + j * 66;
                TextAt(g, r[j * 2], 836, yy, 10, muted, 380, true);
                TextAt(g, r[j * 2 + 1], 836, yy + 20, 15, text, 380);
            }

            Button(g, "Выбрать: " + names[selected] + "   →", 36, 659, 360, 56, OpenCheckout, true);
            Button(g, "Загрузить спрайт PNG", 412, 659, 361, 56, Import);
            Button(g, "Рассмотреть спрайт", 812, 741, 432, 42, () => preview = true);
            Button(g, "Мимикратор · модель", 36, 723, 360, 45, StartMimicratorPreview);
            Button(g, "Мимикратор · бой", 412, 723, 361, 45, StartTestMimicrator);
            Button(g, "Илля против Тохи", 812, 792, 432, 35, StartTestIllyaToha);
            TextAt(g, "Выберите героя и личную реликвию на кассе Евроопта.", 36, 779, 12, muted, 730);
            TextAt(g, notice.Length > 0 ? notice : "← →  выбор      Enter  к кассе      Esc  назад", 36, 805, 12, muted, 1190);
        }
    }
}
