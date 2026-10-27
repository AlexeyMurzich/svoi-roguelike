using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        bool debugConsoleOpen;
        string debugCommand = "", debugReply = "shut_god — Шутен: бесконечное золото, атаки и защита ×3.";
        bool GameplayPaused { get { return debugConsoleOpen || inventoryView != ""; } }

        void SetDebugConsole(bool open)
        {
            debugConsoleOpen = open;
            combatPointer = null;
            routePressed = false;
            hoveredRelic = false;
            CancelMapDrag();
            actions.Clear();
            ResetMotionClock();
            if (open)
            {
                howlSound.Stop();
                if (combatTimer != null) combatTimer.Stop();
            }
            else if (CombatBusy || CombatAmbientMoving || LivingScene || MapMoving || mimicratorPreview)
                WakeMotion();
            SyncMusic();
            Invalidate();
        }

        bool HandleDebugConsoleKey(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Oem3)
            {
                SetDebugConsole(!debugConsoleOpen);
                e.Handled = e.SuppressKeyPress = true;
                return true;
            }
            if (!debugConsoleOpen) return false;
            e.Handled = true;
            if (e.KeyCode == Keys.Escape) SetDebugConsole(false);
            else if (e.KeyCode == Keys.Enter)
            {
                ExecuteDebugCommand(debugCommand.Trim());
                debugCommand = "";
            }
            else if (e.KeyCode == Keys.Back && debugCommand.Length > 0)
                debugCommand = debugCommand.Substring(0, debugCommand.Length - 1);
            e.SuppressKeyPress = e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape || e.KeyCode == Keys.Back;
            Invalidate();
            return true;
        }

        void OnDebugConsoleKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!debugConsoleOpen) return;
            e.Handled = true;
            if (!char.IsControl(e.KeyChar) && debugCommand.Length < 80)
            {
                debugCommand += e.KeyChar;
                Invalidate();
            }
        }

        void ExecuteDebugCommand(string command)
        {
            if (command != "shut_god")
                debugReply = command.Length == 0 ? "Введите команду shut_god." : "Неизвестная команда. Доступна: shut_god.";
            else if (currentHero == null || !mapScreen || currentHero.Hero != 3 || currentHero.Health <= 0)
                debugReply = "Начните забег за Шутена и введите shut_god.";
            else
            {
                currentHero.EnableShutGod();
                debugReply = "shut_god включён: золото ∞, атаки ×3, защита ×3. До конца этого забега.";
            }
            Invalidate();
        }

        void DrawDebugConsole(Graphics g)
        {
            if (!debugConsoleOpen) return;
            actions.Clear();
            float width = CanvasWidth - 32;
            Rect(g, 16, 78, width, 188, Color.FromArgb(248, 10, 25, 24));
            RelicFrame(g, 16, 78, width, 188, true);
            TextAt(g, "КОНСОЛЬ ТЕСТИРОВАНИЯ", 34, 94, 18, gold, width - 190, true);
            TextAt(g, "ё / Esc — закрыть", CanvasWidth - 220, 98, 14, muted, 185);
            TextAt(g, debugReply, 34, 132, 15, text, width - 38);
            Rect(g, 32, 208, width - 32, 40, Color.FromArgb(255, 7, 16, 20));
            TextAt(g, "> " + debugCommand + "▏", 43, 216, 17, gold, width - 55, true);
        }
    }
}
