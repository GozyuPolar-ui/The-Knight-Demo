using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Harvestable : MonoBehaviour
{
    [SerializeField] private int amount = 8;
    [SerializeField] private GameObject inventory;
    [SerializeField] private GameObject droppedItemPrefab;
    [SerializeField] private ToolType harvestableType;
    public void Harvest()
    {
        --amount;
        bool broken = amount <= 0;

        PlayHarvestSound(broken);

        if(broken)
        {
            Destroy(gameObject);
        }

        var spawner = inventory.GetComponent<SpawnItem>();

        if (spawner != null)
        {
            spawner.CreateItem(droppedItemPrefab);
            Debug.Log("success");

        }
    }

    private void PlayHarvestSound(bool broken)
    {
        switch (harvestableType)
        {
            case ToolType.Axe:
                AudioManager.Play(broken ? Sfx.TreeBreak : Sfx.TreeHit);
                break;
            case ToolType.Pickaxe:
                AudioManager.Play(broken ? Sfx.StoneBreak : Sfx.StoneHit);
                break;
        }
    }

    public ToolType GetToolType()
    {
        return harvestableType;
    }

    public void SetInventory(GameObject inventory)
    {
        this.inventory = inventory;
    }   
}
