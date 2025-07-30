using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UiControl : MonoBehaviour
{
    public GameObject Inventory;
    public void CloseInv()
    {
        Inventory.SetActive(false);
        //InventoryManager.Instance.ClearList();
        FirstPersonController.instance.isInventoryOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
