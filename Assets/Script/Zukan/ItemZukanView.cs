using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// �A�C�e���}�ӁiZukanI �V�[���j�̃��C���r���[�B
/// ��W������4�{�^���ŐؑցA�I�𒆃W�������̏��W��������
/// �u���o���� �� �A�C�e��5��O���b�h�v�̏��� VerticalLayoutGroup �֓��I��������B
///
/// �V�[���\���i�z��j:
///   Canvas
///     �� GoZukan(����)/weapon(����)/magic(������)/hojo(�p�b�V�u) �c ��W�������{�^��
///     �� Scroll View
///     ��   �� Viewport
///     ��       �� Content (VerticalLayoutGroup + ContentSizeFitter)
///     ��           �� (���o���с^�O���b�h�u���b�N�𓮓I����)
///     �� �߂� Button
///
/// ������Ԃ� GameState.IsItemDiscovered �Ŕ���B�������́u�H�v�\���B
/// </summary>
public class ItemZukanView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ItemZukanCategoryDatabase database;

    [Header("Major Category Buttons")]
    [Tooltip("��W�������{�^���Bdatabase.majorCategories �Ɠ������я���4�A�T�C������B\n"
           + "[0]���� [1]���� [2]������ [3]�p�b�V�u")]
    [SerializeField] private Button[] majorButtons;

    [Header("Scroll")]
    [Tooltip("�ꗗ�� ScrollRect�i�ڍׂ���߂����ۂ̃X�N���[���ʒu�����Ɏg�p�j")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("VerticalLayoutGroup ������ Content�B���o���тƃO���b�h�u���b�N�̐e�B")]
    [SerializeField] private RectTransform content;

    [Header("Prefabs")]
    [Tooltip("���o���� Prefab�iItemSectionHeaderCell �t���j")]
    [SerializeField] private ItemSectionHeaderCell headerPrefab;

    [Tooltip("�A�C�e���Z�� Prefab�iItemIconCell �t���j")]
    [SerializeField] private ItemIconCell itemCellPrefab;

    [Header("Grid Settings")]
    [Tooltip("�e���W�������O���b�h�̗�")]
    [SerializeField] private int columns = 5;

    [Tooltip("�Z���T�C�Y�iMonsterZukan �ɍ��킹��ꍇ 250�~300�j")]
    [SerializeField] private Vector2 cellSize = new Vector2(250f, 300f);

    [Tooltip("�Z���Ԋu")]
    [SerializeField] private Vector2 spacing = new Vector2(40f, 40f);

    [Header("Back")]
    [SerializeField] private Button backButton;

    [Tooltip("�߂��V�[�����i�}�Ӄg�b�v�j")]
    [SerializeField] private string zukanTopSceneName = "Zukan";

    // �������
    private int currentMajorIndex = 0;
    private readonly List<GameObject> spawned = new List<GameObject>();

    // CanvasGroup on Content: hidden (alpha 0) during tab switch until layout settles.
    private CanvasGroup contentCanvasGroup;
    // Tracks the reveal coroutine so rapid tab taps don't reveal mid-rebuild.
    private Coroutine revealRoutine;

    private void Start()
    {
        // ��W�������{�^���Ƀ��X�i�[�o�^
        if (majorButtons != null)
        {
            for (int i = 0; i < majorButtons.Length; i++)
            {
                int idx = i; // �N���[�W���΍�
                if (majorButtons[i] != null)
                    majorButtons[i].onClick.AddListener(() => OnMajorClicked(idx));
            }
        }

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        // Get/add a CanvasGroup on Content to hide layout reshuffle during tab switches.
        if (content != null)
        {
            contentCanvasGroup = content.GetComponent<CanvasGroup>();
            if (contentCanvasGroup == null)
                contentCanvasGroup = content.gameObject.AddComponent<CanvasGroup>();
        }

        // �ڍׂ���߂������ǂ����ŏ����\���𕪊�
        if (ItemZukanContext.ReturningFromDetail)
        {
            currentMajorIndex = ItemZukanContext.ReturnMajorIndex;
            ItemData target = ItemZukanContext.ReturnTargetItem;

            BuildCategory(currentMajorIndex);
            UpdateButtonVisual();

            if (target != null)
                StartCoroutine(ScrollToTargetNextFrame(target));

            // �t���O�͎g���؂�
            ItemZukanContext.ReturningFromDetail = false;
            ItemZukanContext.ReturnTargetItem = null;
        }
        else
        {
            // �g�b�v���痈���ꍇ: �擪�̑�W�������E�擪�\��
            currentMajorIndex = 0;
            BuildCategory(currentMajorIndex);
            UpdateButtonVisual();
        }
    }

    // =========================================================
    // ��W�������ؑ�
    // =========================================================

    private void OnMajorClicked(int majorIndex)
    {
        currentMajorIndex = majorIndex;

        // Hide content while the new layout settles (avoids 1-frame flicker).
        if (contentCanvasGroup != null)
            contentCanvasGroup.alpha = 0f;

        BuildCategory(majorIndex);
        UpdateButtonVisual();

        // �^�u�ؑ֎��͐擪��
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;

        // Reveal once the nested layout has settled (same idiom as ScrollToTargetNextFrame).
        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = StartCoroutine(RevealAfterLayout());
    }

    /// <summary>
    /// After a tab switch, wait for the layout to settle, then reset scroll to top
    /// and restore the content CanvasGroup. Same wait idiom as ScrollToTargetNextFrame.
    /// </summary>
    private IEnumerator RevealAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;

        if (contentCanvasGroup != null)
            contentCanvasGroup.alpha = 1f;

        revealRoutine = null;
    }

    /// <summary>�I�𒆃{�^���̌����ڂ��X�V�i�Ȉ�: interactable �ŕ\���j�B</summary>
    private void UpdateButtonVisual()
    {
        if (majorButtons == null) return;
        for (int i = 0; i < majorButtons.Length; i++)
        {
            if (majorButtons[i] != null)
                majorButtons[i].interactable = (i != currentMajorIndex);
        }
    }

    // =========================================================
    // �O���b�h�\�z
    // =========================================================

    /// <summary>
    /// �w���W�������̓��e�� Content �ɍč\�z����B
    /// ���W���������ƂɁu���o���� �� �A�C�e��5��O���b�h�v���c�ɐςށB
    /// </summary>
    private void BuildCategory(int majorIndex)
    {
        // ������j��
        foreach (var go in spawned)
        {
            if (go != null)
            {
                go.SetActive(false); // exclude old cells from layout immediately (deferred Destroy)
                Destroy(go);
            }
        }
        spawned.Clear();

        if (database == null || content == null) return;
        if (majorIndex < 0 || majorIndex >= database.majorCategories.Count) return;

        var major = database.majorCategories[majorIndex];
        if (major == null || major.subCategories == null) return;

        foreach (var sub in major.subCategories)
        {
            if (sub == null) continue;

            // --- ���o���� ---
            if (headerPrefab != null)
            {
                var header = Instantiate(headerPrefab, content);
                header.Setup(sub.headerText);
                spawned.Add(header.gameObject);
            }

            // --- �A�C�e���O���b�h�i���̏��W��������p�̓��ꕨ�𓮓I�����j ---
            var gridGo = CreateGridBlock();
            spawned.Add(gridGo);

            if (sub.items != null)
            {
                foreach (var item in sub.items)
                {
                    if (item == null) continue;
                    var cell = Instantiate(itemCellPrefab, gridGo.transform);
                    bool discovered = GameState.I != null && GameState.I.IsItemDiscovered(item.itemId);
                    cell.Setup(item, discovered, OnItemClicked);
                }
            }
        }
    }

    /// <summary>
    /// ���W������1���̃A�C�e������ׂ�A5�� GridLayoutGroup �̓��ꕨ�𐶐�����B
    /// �����̓A�C�e�����ɉ����� ContentSizeFitter �Ŏ��������B
    /// </summary>
    private GameObject CreateGridBlock()
    {
        var go = new GameObject("SubCategoryGrid",
            typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(content, false);

        var grid = go.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;

        // �O���b�h���g�̍����𒆐g�ɍ��킹�ĐL�΂��i�c�ς݂Ő������m�ۂ��邽�߁j
        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go;
    }

    // =========================================================
    // �A�C�e���^�b�v �� �ڍׂ�
    // =========================================================

    private void OnItemClicked(ItemData item)
    {
        if (item == null) return;

        // �����ړ��p: ���݂̑�W���������̔����ς݃A�C�e����1��
        var flat = database.GetFlatItems(currentMajorIndex);
        var discoveredList = new List<ItemData>();
        foreach (var it in flat)
        {
            if (it != null && GameState.I != null && GameState.I.IsItemDiscovered(it.itemId))
                discoveredList.Add(it);
        }

        int index = discoveredList.IndexOf(item);
        if (index < 0) index = 0;

        ItemZukanContext.SelectedItem = item;
        ItemZukanContext.DiscoveredList = discoveredList;
        ItemZukanContext.CurrentIndex = index;
        ItemZukanContext.CurrentMajorIndex = currentMajorIndex;

        SceneManager.LoadScene("Istatus");
    }

    // =========================================================
    // �X�N���[������
    // =========================================================

    /// <summary>
    /// 1�t���[���҂��ă��C�A�E�g�m���A�ΏۃA�C�e���̃Z������ʓ��Ɏ��܂�悤
    /// �X�N���[���ʒu�𒲐�����B���o���т�O���b�h�̍������������đΏۃZ����Y�ʒu�����߂�B
    /// </summary>
    private IEnumerator ScrollToTargetNextFrame(ItemData target)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
            yield break;

        // �ΏۃZ���� RectTransform ��T��
        RectTransform targetCell = FindCellOf(target);
        if (targetCell == null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            yield break;
        }

        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;
        if (contentHeight <= viewportHeight)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            yield break;
        }

        // �ΏۃZ���̒��S�́AContent ��[����̋��������߂�
        // �iContent �� Pivot Y=1 / ��l�߂�z��j
        Vector3[] contentCorners = new Vector3[4];
        Vector3[] cellCorners = new Vector3[4];
        scrollRect.content.GetWorldCorners(contentCorners);
        targetCell.GetWorldCorners(cellCorners);

        float contentTopY = contentCorners[1].y; // ����
        float cellCenterY = (cellCorners[1].y + cellCorners[0].y) * 0.5f; // ����ƍ����̒��_

        // ���[���hY �� Content ��[����̋����i�s�N�Z�����Z�� Canvas scale ���l���j
        float canvasScale = scrollRect.content.lossyScale.y;
        if (Mathf.Approximately(canvasScale, 0f)) canvasScale = 1f;

        float distanceFromTop = (contentTopY - cellCenterY) / canvasScale;

        // �Z�����S���r���[�|�[�g�����ɒu������
        float targetTop = distanceFromTop - viewportHeight * 0.5f;
        float maxScroll = contentHeight - viewportHeight;
        float normalizedFromTop = Mathf.Clamp01(targetTop / maxScroll);

        scrollRect.verticalNormalizedPosition = 1f - normalizedFromTop;
    }

    /// <summary>�����ς݃Z���̒�����A�w��A�C�e���̃Z���� RectTransform ��T���B</summary>
    private RectTransform FindCellOf(ItemData target)
    {
        foreach (var go in spawned)
        {
            if (go == null) continue;
            var cells = go.GetComponentsInChildren<ItemIconCell>();
            foreach (var c in cells)
            {
                if (c != null && c.RepresentsItem(target))
                    return c.transform as RectTransform;
            }
        }
        return null;
    }

    // =========================================================
    // �߂�
    // =========================================================

    private void OnBackClicked()
    {
        SceneManager.LoadScene(zukanTopSceneName);
    }
}