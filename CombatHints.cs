using System;
using System.Collections.Generic;

namespace ShpilSvoih
{
    // Presentation only: combat rules remain in CombatState.
    public sealed class CombatHint
    {
        public readonly int Icon;
        public readonly string Number, Title, Description;
        public CombatHint(int icon, string number, string title, string description)
        {
            Icon = icon; Number = number; Title = title; Description = description;
        }
    }

    public static class CombatHints
    {
        public static List<CombatHint> Hero(CombatState fight)
        {
            var result = new List<CombatHint>();
            var hero = fight.Hero;
            if (hero.Health <= 0 || (fight.Phase != CombatPhase.Player && fight.Phase != CombatPhase.Enemy)) return result;
            if (fight.WeakTurns > 0)
                result.Add(new CombatHint(0, fight.WeakTurns.ToString(), "Слабость", "Урон атак снижен на 25%, с округлением вниз. Осталось ходов: " + fight.WeakTurns + "."));
            if (fight.StunPending)
                result.Add(new CombatHint(1, "→1", "Оглушение · следующий ход", "В следующем ходу можно сыграть только одну карту. Добор остаётся обычным."));
            else if (fight.IsStunned)
                result.Add(new CombatHint(1, Math.Max(0, 1 - fight.CardsPlayedThisTurn).ToString(), "Оглушение", "Карт осталось в этом ходу: " + Math.Max(0, 1 - fight.CardsPlayedThisTurn) + ". Учитываются бесплатные и автоматические карты."));
            if (fight.Rabies)
                result.Add(new CombatHint(2, "∞", "Бешенство", "+2 силы, −2 ловкости. До конца боя. Не складывается. Сила усиливает атаки, ловкость уменьшает получаемый картами блок."));
            if (fight.Turn == 1 && hero.Effects.Strength > 0)
                result.Add(new CombatHint(3, "+" + hero.Effects.Strength, "Сила", "Урон увеличен на " + hero.Effects.Strength + " в первом раунде боя."));
            if (hero.Hero == 0)
                result.Add(new CombatHint(5, hero.Tears.ToString(), "Слёзы", "Слёзы накапливаются от потери HP. Текущая прибавка к урону: " + hero.BonusDamage + "."));
            if (hero.Effects.NegatesFirstTurn)
                result.Add(new CombatHint(7, "1", "Саттен-киссюн", "Весь урон и дебафы первого хода боя отменяются."));
            if (hero.Hero == 1)
                result.Add(new CombatHint(16, "", "Замешательство", "Добор +2. В начале хода две случайные карты из руки разыгрываются автоматически. Оглушение ограничивает и автоматические розыгрыши."));
            if (hero.Hero == 3 && hero.Debt > 0)
                result.Add(new CombatHint(17, hero.Debt.ToString("0.#"), "Долг", "Разрешён мобильным телефоном. Рост долга отнимает HP. Возврат денег не лечит."));
            if (hero.Hero == 2 && hero.FamilyThisTurn != CardFamily.None)
                result.Add(new CombatHint(18, "", "Тип карт хода", hero.FamilyThisTurn == CardFamily.Feminist ? "В этом ходу доступны только феминистические и нейтральные карты." : "В этом ходу доступны только патриархальные и нейтральные карты."));
            return result;
        }

        public static List<CombatHint> Intent(CombatEnemy enemy)
        {
            var result = new List<CombatHint>();
            if (!enemy.IsAlive) return result;
            if (enemy.Kind == EnemyKind.Mimicrator)
            {
                var form = enemy.IsActing ? enemy.Form : enemy.NextForm;
                string prefix = enemy.IsActing ? "Текущая форма. " : "В начале своего хода примет эту форму. ";
                if (form == MimicratorForm.Fairy)
                    result.Add(new CombatHint(14, "", "Фея · отдых", prefix + "Отдыхает: не атакует и не лечится."));
                else if (form == MimicratorForm.Beacon)
                {
                    result.Add(new CombatHint(11, "6", "Мигалка · удар", prefix + "Нанесёт 6 урона."));
                    result.Add(new CombatHint(1, "", "Оглушение", "При превращении оглушит на следующий ход игрока: можно сыграть только одну карту. Полный блок не отменяет оглушение."));
                }
                else if (form == MimicratorForm.Bear)
                    result.Add(new CombatHint(12, "14", "Мишка · тяжёлый удар", prefix + "Нанесёт 14 урона."));
                else
                {
                    result.Add(new CombatHint(13, "5 × 2", "Мидия · мультиатака", prefix + "Два отдельных удара по 5. Блок расходуется последовательно."));
                    result.Add(new CombatHint(15, "12", "Мидия · защита", "Сразу при превращении получит 12 блока, затем атакует."));
                }
            }
            else if (enemy.WillHowl)
                result.Add(new CombatHint(10, "", "Вой · слабость", "Наложит слабость на следующий ход: урон атак снижен на 25%. Вой не наносит урон."));
            else
            {
                bool needles = enemy.Kind == EnemyKind.Hedgehog;
                result.Add(new CombatHint(needles ? 9 : 8, enemy.AttackDamage.ToString(), needles ? "Иглы" : "Укус", "Нанесёт " + enemy.AttackDamage + " урона."));
                if (enemy.Kind == EnemyKind.RabidHedgehog)
                    result.Add(new CombatHint(2, "", "Заражение бешенством", "Если укус отнимет HP, наложит бешенство до конца боя: +2 силы, −2 ловкости. Полный блок защищает от заражения."));
            }
            return result;
        }
    }
}
