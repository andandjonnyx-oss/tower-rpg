using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TowerEntranceView : MonoBehaviour
{
    [Header("Back to Main")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    private FloorButton[] floorButtons;

    private void Awake()
    {
        floorButtons = GetComponentsInChildren<FloorButton>(includeInactive: true);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void Start()
    {
        RefreshButtons();

        // Controller support: wire the visible floor buttons + back into a
        // vertical loop and set the initial focus. Runs after a frame so the
        // buttons' active/inactive state (from Refresh) is settled.
        StartCoroutine(WireNavAfterLayout());
    }

    private void RefreshButtons()
    {
        var gs = GameState.I;
        int reached = (gs != null) ? gs.reachedFloor : 1;

        foreach (var fb in floorButtons)
        {
            if (fb != null)
                fb.Refresh(reached);
        }
    }

    // =========================================================
    // Controller / keyboard
    // =========================================================

    private IEnumerator WireNavAfterLayout()
    {
        yield return null;
        WireNav();
    }

    private void WireNav()
    {
        // Collect visible, interactable floor buttons sorted top->bottom, left->right.
        var list = new List<Selectable>();
        if (floorButtons != null)
        {
            foreach (var fb in floorButtons)
            {
                if (fb == null) continue;
                var b = fb.GetComponent<Button>();
                if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
                list.Add(b);
            }
        }
        list.Sort((a, c) =>
        {
            float ay = a.transform.position.y, cy = c.transform.position.y;
            if (!Mathf.Approximately(ay, cy)) return cy.CompareTo(ay);
            return a.transform.position.x.CompareTo(c.transform.position.x);
        });

        // Group into rows by Y.
        var rows = new List<List<Selectable>>();
        List<Selectable> cur = null;
        float curY = 0f;
        foreach (var s in list)
        {
            float y = s.transform.position.y;
            if (cur == null || Mathf.Abs(y - curY) > 20f)
            {
                cur = new List<Selectable>();
                rows.Add(cur);
                curY = y;
            }
            cur.Add(s);
        }

        Selectable back = (backButton != null && backButton.isActiveAndEnabled) ? backButton : null;

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (int i = 0; i < row.Count; i++)
            {
                var cell = row[i];
                float x = cell.transform.position.x;
                Selectable left = (i > 0) ? row[i - 1] : null;
                Selectable right = (i < row.Count - 1) ? row[i + 1] : null;
                Selectable up = (r > 0) ? NearestByX(rows[r - 1], x) : back;      // top row up -> back
                Selectable down = (r < rows.Count - 1) ? NearestByX(rows[r + 1], x) : back; // bottom row down -> back
                ControllerNav.SetExplicit(cell, up, down, left, right);
            }
        }

        if (back != null)
        {
            Selectable topLeft = (rows.Count > 0 && rows[0].Count > 0) ? rows[0][0] : null;
            Selectable bottomLeft = (rows.Count > 0 && rows[rows.Count - 1].Count > 0) ? rows[rows.Count - 1][0] : null;
            ControllerNav.SetExplicit(back, bottomLeft, topLeft, null, null); // up->bottom row, down->top row
        }

        SelectionHighlighter.PreferredFallback =
            (rows.Count > 0 && rows[0].Count > 0) ? rows[0][0] : back;
    }

    private static Selectable NearestByX(List<Selectable> row, float x)
    {
        Selectable best = null; float bestD = float.MaxValue;
        for (int i = 0; i < row.Count; i++)
        {
            if (row[i] == null) continue;
            float d = Mathf.Abs(row[i].transform.position.x - x);
            if (d < bestD) { bestD = d; best = row[i]; }
        }
        return best;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame)
            || (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            OnBackClicked();
        }
    }

    private void OnBackClicked()
    {
        SceneManager.LoadScene(mainSceneName);
    }
}
