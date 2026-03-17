using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BattleSceneController : MonoBehaviour
{
    [Header("UI")]
    public Image enemyImage;

    [Header("Scene Names")]
    public string towerSceneName = "Tower"; // ‚ ‚È‚½‚Ì“ƒƒV[ƒ“–¼‚É‡‚í‚¹‚Ä•ÏX

    private void Start()
    {
        var m = BattleContext.EnemyMonster;
        if (m == null)
        {
            Debug.LogError("[Battle] EnemyMonster is null");
            return;
        }

        enemyImage.sprite = m.Image;
        enemyImage.preserveAspect = true;
    }

    // UI Button‚©‚çŒÄ‚Ô
    public void EndBattleAndReturn()
    {
        SceneManager.LoadScene(towerSceneName, LoadSceneMode.Single);
    }
}