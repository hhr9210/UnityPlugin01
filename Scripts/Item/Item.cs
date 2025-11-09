using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Item", menuName = "Item", order = 0)]
public class Item : ScriptableObject
{
    public enum Category { Consumable, Equip, Material, Scroll, Task }
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
    public enum EquipCatergory{None,Head,Chest,Hand,Waist,Leg,Feet,weapon}



    // [Header("Basic Attributes")]
    public string itemName;
    public Category category = Category.Consumable;
    public Rarity rarity = Rarity.Common;
    [Range(1,9999)]
    public float price;
    [Range(0f,9999f)]
    public float weight;
    public bool isStackable = true;
    [Range(0,99)]
    public int maxStackSize = 99;



    public Sprite icon;
    public GameObject model;
    public Material material;
    [TextArea(3, 10)]
    public string description;




    // [Header("Equipment Attributes")]
    public EquipCatergory equipCatergory = EquipCatergory.None;
    [Range(0f, 100f)]
    public int bonusAttack;
    [Range(0f, 100f)]
    public int bonusDefend;
    [Range(0f, 100f)]
    public int bonusSpeed;
    [Range(0f, 100f)]
    public int bonusIntelligence;
    




    public virtual void Use()
    {
        Debug.Log($"使用了物品{itemName}");
    }


}

