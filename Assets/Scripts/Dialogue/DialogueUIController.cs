using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueUIController : MonoBehaviour
{
    [Header("Hook this up")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI lineText;
    [SerializeField] private Transform choicesContainer;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("Hide if any of these are active")]
    [SerializeField] private List<GameObject> suppressWhileActive = new();

    private readonly List<Button> _spawned = new();

    private void OnEnable()
    {
        DialogueService.OnOpen += OnOpen;
        DialogueService.OnAdvance += OnAdvance;
        DialogueService.OnClose += OnClose;

        ShopService.OnShopOpened += OnShopOpened;
        ShopService.OnShopClosed += OnShopClosed;

        if (DialogueService.CurrentDef != null && DialogueService.CurrentNode != null)
            OnOpen(DialogueService.CurrentDef, DialogueService.CurrentNode);
    }

    private void OnDisable()
    {
        DialogueService.OnOpen -= OnOpen;
        DialogueService.OnAdvance -= OnAdvance;
        DialogueService.OnClose -= OnClose;

        ShopService.OnShopOpened -= OnShopOpened;
        ShopService.OnShopClosed -= OnShopClosed;
    }

    private void Update()
    {
        if (DialogueService.IsAutoClosing && Time.unscaledTime >= DialogueService.AutoCloseAt)
        {
            DialogueService.End();
            return;
        }

        if (DialogueService.CurrentDef != null)
        {
            bool shouldHide = false;
            for (int i = 0; i < suppressWhileActive.Count; i++)
            {
                var go = suppressWhileActive[i];
                if (go && go.activeInHierarchy) { shouldHide = true; break; }
            }
            if (shouldHide) Hide();
            else Show();
        }
    }

    private void OnShopOpened(Trader _)
    {
        Hide();
    }

    private void OnShopClosed()
    {
        if (DialogueService.CurrentDef != null) Show();
    }

    private void OnOpen(DialogueDefinition def, DialogueNode node)
    {
        if (panelRoot) panelRoot.SetActive(true);
        //Cursor.lockState = CursorLockMode.None;
        //Cursor.visible = true;
        Show();
        Rebuild(def, node);
    }

    private void OnAdvance(DialogueDefinition def, DialogueNode node)
    {
        Rebuild(def, node);
    }

    private void OnClose()
    {
        Hide();
        if (panelRoot) panelRoot.SetActive(false);
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    private void Rebuild(DialogueDefinition def, DialogueNode node)
    {
        if (!panelRoot) return;

        nameText?.SetText(def ? def.npcName : "—");
        lineText?.SetText(node != null ? node.line : "");

        for (int i = 0; i < _spawned.Count; i++) if (_spawned[i]) Destroy(_spawned[i].gameObject);
        _spawned.Clear();

        if (node == null) return;

        if (node.choices == null || node.choices.Count == 0)
        {
            if (!DialogueService.IsAutoClosing)
            {
                var b = Instantiate(choiceButtonPrefab, choicesContainer);
                b.GetComponentInChildren<TextMeshProUGUI>().SetText("OK");
                b.onClick.AddListener(DialogueService.End);
                _spawned.Add(b);
            }
            return;
        }

        for (int i = 0; i < node.choices.Count; i++)
        {
            var ch = node.choices[i];
            var b = Instantiate(choiceButtonPrefab, choicesContainer);
            b.GetComponentInChildren<TextMeshProUGUI>().SetText(ch.label);
            b.onClick.AddListener(() => DialogueService.Choose(ch));
            _spawned.Add(b);
        }
    }

    private void Show() { if (panelRoot && !panelRoot.activeSelf) panelRoot.SetActive(true); }
    private void Hide() { if (panelRoot && panelRoot.activeSelf) panelRoot.SetActive(false); }
}