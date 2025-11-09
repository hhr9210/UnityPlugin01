using UnityEngine;
using System.Collections.Generic;

// 这是一个独立的 ScriptableObject 资产，用于存储一个完整的物品栏数据。
public class Inventory : ScriptableObject
{
    public string inventoryName = "New Player Inventory";
    public List<InventoryItem> items = new List<InventoryItem>();

}


[System.Serializable]
public class InventoryItem
{
    public Item item; // 对 Item ScriptableObject 的引用
    public int quantity = 1;

    public InventoryItem(Item item, int quantity = 1)
    {
        this.item = item;
        this.quantity = quantity;
    }
}