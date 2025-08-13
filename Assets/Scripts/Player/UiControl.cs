using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UiControl : MonoBehaviour
{
    public static UiControl Instance;

    [Header("UI")]
    [SerializeField] private GameObject PopUpText;
    private TextMeshProUGUI message;    
    [SerializeField] private GameObject Inventory;
    [SerializeField] private GameObject Shop;


    [Header("Typing")]
    [SerializeField, Range(1f, 120f)] private float charsPerSecond = 40f;
    [SerializeField] private float holdSecondsAfterType = 2.0f;

    private readonly Queue<string> queue = new();
    private bool isShowing;
    private Coroutine runner;
    private bool isTyping;
    private string currentFull;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else { Destroy(gameObject); return; }
        message = PopUpText.GetComponent<TextMeshProUGUI>();
        if (PopUpText) PopUpText.SetActive(false);
    }

    public void Show(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        queue.Enqueue(text);
        if (!isShowing) runner = StartCoroutine(RunQueue());
    }

    public void SkipOrHide()
    {
        if (!isShowing) return;

        if (isTyping)
        {
            isTyping = false;
            if (message) message.text = currentFull;
        }
        else
        {
            if (PopUpText) PopUpText.SetActive(false);
            isShowing = false;
        }
    }

    private IEnumerator RunQueue()
    {
        isShowing = true;
        while (queue.Count > 0)
        {
            currentFull = queue.Dequeue();
            PopUpText.SetActive(true);

            yield return StartCoroutine(TypeOut(currentFull));

            float t = 0f;
            while (t < holdSecondsAfterType && isShowing)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            PopUpText.SetActive(false);
        }

        isShowing = false;
        runner = null;
    }

    private IEnumerator TypeOut(string full)
    {
        isTyping = true;
        if (message) message.text = "";
        if (charsPerSecond <= 0f) charsPerSecond = 40f;

        float delay = 1f / charsPerSecond;

        for (int i = 0; i < full.Length; i++)
        {
            if (!isTyping) break;
            if (message) message.text += full[i];
            yield return new WaitForSecondsRealtime(delay);
        }

        if (message) message.text = full;
        isTyping = false;
    }

    public void CloseInv()
    {
        Inventory.SetActive(false);
        //InventoryManager.Instance.ClearList();
        FirstPersonController.instance.isInventoryOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void CloseShop()
    {
        ShopService.End();
        if (Shop) Shop.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
