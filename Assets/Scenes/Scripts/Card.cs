using UnityEngine;
using System.Collections.Generic;

public enum CardType { Number, J, Q, D, K, T }
public enum CardCategory { Unit, Spell }
public enum AbilityType { None, Passive, Active, Spell, Arcane }
public enum AttackType { Physical, Magic }
public enum BoardPosition { None, L, P, C, S, G }

[System.Serializable]
public class Card
{
    public string cardName;
    public CardType cardType;
    public int baseValue;
    public int attack;
    public int maxAttack;
    public int health;
    public int maxHealth;
    public int classId;
    public string className;
    public CardCategory category;
    public int actionPointCost;
    public AbilityType abilityType = AbilityType.None;
    public int abilityActivationCost;
    public string triggerCondition;
    public bool hasSpecialAbility;
    public string specialAbilityName;
    public string abilityDescription;
    public int cooldown = 1;
    public bool hasArcaneAlternative;
    public string spellEffectName;
    public string spellEffectDescription;
    public int spellActivationCost;
    public string arcaneEffectName;
    public string arcaneEffectDescription;
    public int arcaneActivationCost;
    public string cardId;
    public int cardIndex = -1;
    public bool isSpecialCard;
    public List<string> providesImmunity = new List<string>();
    public List<string> appliedEffects = new List<string>();

    public int ownerId = -1;
    public BoardPosition boardPosition = BoardPosition.None;
    public int armorPhysical;
    public int armorMagic;
    public AttackType attackType = AttackType.Physical;
    
    public bool requiresSacrifice = false; 
    public int sacrificeAPCost = 0;        
    public bool isSacrificePaid = false;   
    public bool isArcane = false; // ДОБАВЛЕНО

    public bool isNamed;
    public bool isStudied;
    public int currentCooldown;
    public int maxCooldown;
    public int sleepTimer;
    public bool hasAttackedThisTurn;
    public string classBuffEffect = "";

    public Card(string name, CardType type, int value, int classId, int cost,
                CardCategory cat = CardCategory.Unit, int index = -1, bool special = false)
    {
        cardName = name;
        cardType = type;
        baseValue = value;
        this.classId = classId;
        category = cat;
        actionPointCost = cost;
        cardIndex = index;
        isSpecialCard = special;
        abilityDescription = "";
        triggerCondition = "";
        GenerateCardId();

        ownerId = -1;
        boardPosition = BoardPosition.None;
        armorPhysical = 0;
        armorMagic = 0;
        attackType = AttackType.Physical;
        isNamed = (type != CardType.Number);
        isStudied = false;
        currentCooldown = 0;
        maxCooldown = 0;
        sleepTimer = 0;
        hasAttackedThisTurn = false;
        classBuffEffect = "";
    }

    private void GenerateCardId()
    {
        string baseId = $"{cardName}_{cardType}_{baseValue}_{classId}_{cardIndex}";
        int hash = baseId.GetHashCode();
        cardId = $"CARD_{System.Math.Abs(hash):X8}";
    }

    public string GetDisplayInfo()
    {
        if (category == CardCategory.Spell && hasArcaneAlternative)
            return $"{cardName}\n\nЗаклинание: {spellEffectName}\n{spellEffectDescription}\nАркана: {arcaneEffectName}\n{arcaneEffectDescription}";
        if (!string.IsNullOrEmpty(abilityDescription))
            return $"{cardName}\n\n{abilityDescription}";
        return cardName;
    }

    public void ResetTurnFlags() => hasAttackedThisTurn = false;
}