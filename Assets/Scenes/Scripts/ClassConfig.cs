using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Class_", menuName = "Cards/Class Config")]
public class ClassConfig : ScriptableObject
{
    public string className;
    public int classId;

    public UnitCardSettings[] unitCards = new UnitCardSettings[5];
    public SpellCardSettings[] spellCards = new SpellCardSettings[5]; // Возвращено на 5
    public SpecialCardSettings[] specialCards = new SpecialCardSettings[5];

    public List<Card> GenerateAllCards()
    {
        List<Card> cards = new List<Card>();
        for (int i = 0; i < unitCards.Length; i++)
            if (unitCards[i] != null) cards.Add(CreateUnitCard(i + 1, unitCards[i]));

        CardType[] specialTypes = { CardType.J, CardType.Q, CardType.D, CardType.K, CardType.T };
        int[] specialValues = { 11, 12, 13, 14, 15 };

        for (int i = 0; i < specialCards.Length; i++)
            if (specialCards[i] != null) 
                cards.Add(CreateSpecialCard(specialTypes[i], specialValues[i], specialCards[i], i));

        for (int i = 0; i < spellCards.Length; i++)
            if (spellCards[i] != null) 
                cards.Add(CreateSpellCard(6 + i, spellCards[i]));

        return cards;
    }

    private Card CreateUnitCard(int value, UnitCardSettings settings)
    {
        string name = !string.IsNullOrEmpty(settings.cardName) ? settings.cardName : "";
        Card card = new Card(name, CardType.Number, value, classId,
            settings.actionPointCost, CardCategory.Unit, value - 1, false)
        {
            attack = settings.attack,
            maxAttack = settings.attack,
            health = settings.health,
            maxHealth = settings.health,
            className = this.className,
            armorPhysical = settings.armorPhysical,
            armorMagic = settings.armorMagic,
            attackType = settings.attackType,
            requiresSacrifice = settings.requiresSacrifice,
            sacrificeAPCost = settings.sacrificeAPCost,
            isNamed = settings.isNamed,
            abilityDescription = settings.abilityDescription
        };
        return card;
    }

    private Card CreateSpecialCard(CardType cardType, int value, SpecialCardSettings settings, int index)
    {
        string name = !string.IsNullOrEmpty(settings.cardName) ? settings.cardName : "";
        Card card = new Card(name, cardType, value, classId,
            settings.actionPointCost, CardCategory.Unit, index, true)
        {
            attack = settings.attack,
            maxAttack = settings.attack,
            health = settings.health,
            maxHealth = settings.health,
            className = this.className,
            armorPhysical = settings.armorPhysical,
            armorMagic = settings.armorMagic,
            attackType = settings.attackType,
            requiresSacrifice = settings.requiresSacrifice,
            sacrificeAPCost = settings.sacrificeAPCost,
            isNamed = settings.isNamed,
            abilityDescription = settings.abilityDescription
        };
        return card;
    }

    private Card CreateSpellCard(int value, SpellCardSettings settings)
    {
        string name = !string.IsNullOrEmpty(settings.cardName) ? settings.cardName : "";
        Card card = new Card(name, CardType.Number, value, classId,
            settings.actionPointCost, CardCategory.Spell, (value - 6), false)
        {
            className = this.className,
            isArcane = settings.isArcane,
            abilityDescription = settings.description
        };
        return card;
    }

    public Sprite GetCardImage(Card card)
    {
        if (card == null) return null;
        if (card.isSpecialCard && card.cardIndex >= 0 && card.cardIndex < specialCards.Length)
            return specialCards[card.cardIndex]?.cardImage;
        if (card.category == CardCategory.Spell && card.cardIndex >= 0 && card.cardIndex < spellCards.Length)
            return spellCards[card.cardIndex]?.cardImage;
        if (card.cardIndex >= 0 && card.cardIndex < unitCards.Length)
            return unitCards[card.cardIndex]?.cardImage;
        return null;
    }
}

[System.Serializable]
public class UnitCardSettings
{
    public string cardName = "";
    public Sprite cardImage;
    [Range(0, 20)] public int attack = 1;
    [Range(0, 20)] public int health = 2;
    [Range(0, 10)] public int actionPointCost = 1;
    [TextArea(1, 3)] public string abilityDescription = "";
    [Range(0, 5)] public int armorPhysical = 0;
    [Range(0, 5)] public int armorMagic = 0;
    public AttackType attackType = AttackType.Physical;
    public bool requiresSacrifice = false;
    [Range(0, 5)] public int sacrificeAPCost = 0;
    public bool isNamed = false;
}

[System.Serializable]
public class SpellCardSettings
{
    public string cardName = "";
    public Sprite cardImage;
    [TextArea(2, 4)] public string description = "";
    [Range(0, 10)] public int actionPointCost = 1;
    public bool isArcane = false;
}

[System.Serializable]
public class SpecialCardSettings
{
    public string cardName = "";
    public Sprite cardImage;
    [Range(0, 20)] public int attack = 3;
    [Range(0, 20)] public int health = 5;
    [Range(0, 10)] public int actionPointCost = 2;
    [TextArea(1, 3)] public string abilityDescription = "";
    [Range(0, 5)] public int armorPhysical = 0;
    [Range(0, 5)] public int armorMagic = 0;
    public AttackType attackType = AttackType.Physical;
    public bool requiresSacrifice = false;
    [Range(0, 5)] public int sacrificeAPCost = 0;
    public bool isNamed = true;
}