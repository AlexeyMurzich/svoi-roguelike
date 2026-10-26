using System;
using System.Windows.Forms;

namespace ShpilSvoih
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                using (var game = new Game())
                {
                    game.StartFullscreen();
                    if (Array.IndexOf(args, "--test-battle") >= 0)
                        game.StartTestBattle();
                    if (Array.IndexOf(args, "--test-dogs") >= 0)
                        game.StartTestDogs();
                    if (Array.IndexOf(args, "--test-hedgehog") >= 0) game.StartTestHedgehog();
                    if (Array.IndexOf(args, "--test-rabid-hedgehog") >= 0) game.StartTestRabidHedgehog();
                    if (Array.IndexOf(args, "--test-hedgehogs") >= 0) game.StartTestHedgehogs();
                    if (Array.IndexOf(args, "--preview-mimicrator") >= 0) game.StartMimicratorPreview();
                    if (Array.IndexOf(args, "--test-mimicrator") >= 0) game.StartTestMimicrator();
                    Application.Run(game);
                }

                return 0;
            }
            catch (Exception error)
            {
                MessageBox.Show("Не удалось запустить игру.\n\n" + error.Message, "Свои люди", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
